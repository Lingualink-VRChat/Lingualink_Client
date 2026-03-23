# LinguaLink Auth Server 开发规划（Casdoor OAuth2/OIDC）

> 版本: 1.1.0  
> 语言: Go 1.25+  
> 框架: Gin  
> 数据库: PostgreSQL 16+  
> 缓存: Redis 7+  
> 认证: Casdoor（OAuth 2.0 / OIDC）

---

## 1. 项目概述

### 1.1 项目定位

LinguaLink Auth Server 是 LinguaLink 生态系统的 **计费/订阅** 中心服务：

- **身份认证委托 Casdoor**：用户身份（登录、OAuth、密码）由 Casdoor 统一提供
- **本服务仅管理业务数据**：订阅套餐、订单、订阅有效期校验（不再包含 API Key）
- **翻译服务无状态**：Translation Server 每次请求调用 Auth Server 校验 Bearer Token 与订阅有效期

### 1.2 系统架构

```
┌──────────────────────────────────────────────┐
│              LinguaLink Client               │
│   (WPF / Web / Mobile / Third-party Client)  │
└───────────────────────┬──────────────────────┘
                        │
                        │ 1) 登录 / 获取 code
                        ▼
                 ┌────────────┐
                 │  Casdoor    │
                 │ (OAuth/OIDC)│
                 └─────┬──────┘
                       │ 2) 回调携带 code
                       ▼
┌──────────────────────────────────────────────┐
│           LinguaLink Auth Server              │
│ - code 换 token + 验证 token                  │
│ - 查找/创建本地 users（id = casdoor user_id） │
│ - 签发业务 JWT（用于 Auth Server 的业务接口） │
│ - 订阅 / 支付 / 订阅有效期校验               │
└───────────────┬──────────────────────────────┘
                │
   ┌────────────┴────────────┐
   │                         │
   ▼                         ▼
┌──────────┐            ┌──────────┐
│PostgreSQL│            │  Redis   │
└──────────┘            └──────────┘

Translation Server（无状态）：
  - Client 请求携带 Authorization: Bearer <user_access_token>
  - Translation Server 使用 X-Internal-Key 调 Auth Server 内部接口，并透传 Authorization
```

### 1.3 技术栈

| 组件 | 技术选型 | 版本 |
|------|---------|------|
| 语言 | Go | 1.25+ |
| Web 框架 | Gin | 1.9.x |
| ORM | GORM | 1.25.x |
| 数据库 | PostgreSQL | 16+ |
| 缓存 | Redis | 7+ |
| 配置管理 | Viper | 1.18.x |
| 日志 | Zap | 1.27.x |
| 认证 | Casdoor | OAuth2/OIDC |
| 业务 JWT | golang-jwt/jwt | v5 |
| 参数验证 | validator | v10 |
| 支付宝 SDK | smartwalle/alipay | v3 |
| 微信支付 SDK | wechatpay-apiv3/wechatpay-go | 0.2.x |

---

## 2. 项目结构

以 `AGENTS.md` 为准，建议结构（示意）：

```
cmd/server/main.go
internal/
  config/
  model/           # users/subscriptions/orders...
  repository/
  service/
  handler/
  middleware/
  pkg/
    casdoor/       # Casdoor SDK 封装（token 换取/校验/userinfo）
    payment/
    response/
web/
  user-portal/
  admin-portal/
```

---

## 3. 数据库设计

### 3.1 核心表

| 表 | 说明 |
|---|------|
| `users` | 用户（`id = casdoor user_id`，不存密码） |
| `subscription_plans` | 套餐定义 |
| `user_subscriptions` | 用户订阅 |
| `payment_orders` | 支付订单 |
| `refresh_tokens` | 刷新令牌（可选：Postgres 或 Redis） |

### 3.2 users - 用户表（与 Casdoor 关联）

```sql
CREATE TABLE users (
    id              VARCHAR(36) PRIMARY KEY,  -- 与 Casdoor user_id 一致
    casdoor_name    VARCHAR(100),
    display_name    VARCHAR(100),
    avatar_url      VARCHAR(512),
    email           VARCHAR(255),
    status          VARCHAR(20) NOT NULL DEFAULT 'active',
    created_at      TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_users_email ON users(email) WHERE email IS NOT NULL;
CREATE INDEX idx_users_status ON users(status);
```

> 说明：用户详细信息以 Casdoor 为准；本地字段仅做冗余缓存与业务关联。

### 3.3 api_keys 下线说明

- 已移除 `api_keys` 表与相关数据模型/仓储。
- 数据库迁移：`migrations/000002_remove_api_keys.up.sql` 执行 `DROP TABLE api_keys`。
- 上线环境完成迁移后，仍依赖 API Key 的旧客户端会直接不可用，需要升级为 Bearer Token 方案。

### 3.4 订阅/订单表

`subscription_plans` / `user_subscriptions` / `payment_orders` 用于承载套餐定义、订阅有效期与订单。金额字段建议统一使用“分”为单位的整数存储，避免浮点精度问题。

当前计费策略已调整为包月/包年有效期模式：
- 不再使用 `daily_quota` / `used_today` / `remaining`。
- 用户订阅在 `status=active` 且当前时间位于 `start_date~end_date` 时可无限使用。
- 套餐核心字段为 `code/name/price_monthly_cents/price_yearly_cents/features`。

---

## 4. API 接口设计

### 4.1 基础约定

- Base Path：`/api/v1`
- 用户端认证：`Authorization: Bearer <access_token>`
- 内部接口认证：`X-Internal-Key: <internal_api_key>`
- 响应格式：统一 `code/message/data/error`

### 4.1.1 客户端/调用侧注意

- `INTERNAL_API_KEY` 为服务间密钥，仅供 Translation Server/内网服务使用，客户端不得内置或下发。
- `client_callback` 会将 token 放在 URL query，可能泄漏到日志/历史记录/Referer。
  - 桌面端可用本地回调（`http://localhost:xxxxx/callback`）。
  - Web 端建议 `redirect=0` 由服务端回调后返回 JSON 自行接管。
- 收到 `401 unauthorized` 需触发重新登录并清理本地 token；不要做 API Key 重试/轮换逻辑。

### 4.2 错误码（建议调整）

| 错误码 | 名称 | 说明 |
|------:|------|------|
| 0 | success | 成功 |
| 40001 | invalid_params | 参数校验失败 |
| 40101 | unauthorized | 未授权 |
| 40301 | forbidden | 禁止访问 |
| 40401 | not_found | 资源不存在 |
| 40901 | conflict | 资源冲突 |
| 42901 | rate_limited | 触发限流 |
| 50001 | internal_error | 服务器内部错误 |
| 50011 | casdoor_error | Casdoor 调用异常/配置错误 |
| 50021 | payment_error | 支付服务异常 |
| 40311 | subscription_inactive | 订阅未生效或已过期 |

### 4.3 接口分组

#### 4.3.1 公开接口（`/api/v1/public`）

- `GET /health`
- `GET /api/v1/public/health`
- `GET /api/v1/public/plans`（可选：公开展示套餐）

#### 4.3.2 认证接口（`/api/v1/auth`）

- `GET /api/v1/auth/casdoor/login`：跳转到 Casdoor 登录页（302 或返回 `login_url`）
- `GET /api/v1/auth/casdoor/login?redirect=0`：返回 `login_url`，客户端自行打开
- `GET /api/v1/auth/casdoor/callback`：Casdoor 回调（`code/state`），Auth Server 用 code 换 token、校验并签发业务 JWT
  - 未传 `client_callback`：返回 JSON（`access_token`/`refresh_token`/`expires_at`/`user`）
  - 可选：`client_callback=http://localhost:xxxxx/callback`（回调时 token 放在 query）
- `POST /api/v1/auth/refresh`：刷新业务 access_token
- `POST /api/v1/auth/logout`：注销（吊销 refresh token / 加入黑名单，策略二选一）

#### 4.3.3 用户接口（`/api/v1/user`，需要业务 JWT）

- `GET /api/v1/user/profile`：获取当前用户信息（本地用户信息 + 订阅摘要，按服务端实现返回）
- `PUT /api/v1/user/profile`：更新可编辑字段（如本地 `display_name`、头像自定义等）
- `GET /api/v1/user/account-settings`：获取账户设置

#### 4.3.4 订阅接口（`/api/v1/subscription`，需要业务 JWT）

- 订阅/支付独立接口为后续实现项（当前以 `/api/v1/user/profile` 为主）

#### 4.3.5 支付接口（`/api/v1/payment`，需要业务 JWT，回调例外）

- `POST /api/v1/payment/orders`
- `GET /api/v1/payment/orders/:order_id/status`
- `POST /api/v1/payment/alipay/notify`（第三方回调，无业务 JWT）
- `POST /api/v1/payment/wechat/notify`（第三方回调，无业务 JWT）

#### 4.3.6 内部接口（`/api/v1/internal`，仅服务间调用）

Translation Server 调用；必须校验 `X-Internal-Key`，并限制内网访问。

- `POST /api/v1/internal/auth/verify`：验证用户 access_token + 检查订阅是否有效（需透传 Authorization）

---

## 5. 核心业务流程

### 5.1 Casdoor 登录（Authorization Code）

```
Client -> Auth Server:  GET /api/v1/auth/casdoor/login
Auth Server -> Casdoor: 302 跳转（携带 client_id/redirect_uri/state/scope...）
Casdoor -> Auth Server: GET /api/v1/auth/casdoor/callback?code=...&state=...
Auth Server:
  1) 用 code 向 Casdoor 换取 token
  2) 验证 token（签名 + aud + exp 等）
  3) 获取 userinfo（或从 id_token claims）
  4) upsert users(id=casdoor_user_id)
  5) 签发业务 access/refresh token
  6) 返回给 Client（JSON 或重定向/回调携带 token）
```

### 5.2 订阅有效期校验（Translation Server 集成）

```
Translation Server 收到翻译请求（包含用户 access_token）
  1) POST /api/v1/internal/auth/verify  (X-Internal-Key + Authorization: Bearer <user_access_token>)
  2) 若允许 -> 执行翻译
```

---

## 6. 配置文件结构

```yaml
# configs/config.yaml

server:
  host: "0.0.0.0"
  port: 8080
  mode: "debug" # debug / release

database:
  host: "localhost"
  port: 5432
  user: "postgres"
  password: ""
  dbname: "lingualink_auth"
  sslmode: "disable"
  max_open_conns: 10
  max_idle_conns: 5

redis:
  host: "localhost"
  port: 6379
  password: ""
  db: 0

jwt:
  secret: ""            # 业务 JWT secret（生产必须通过环境变量注入）
  access_expire: "24h"
  refresh_expire: "168h"

casdoor:
  endpoint: ""
  client_id: ""
  client_secret: ""
  organization: ""
  application: ""
  certificate: |         # 用于验证 Casdoor JWT（建议配置）
    -----BEGIN CERTIFICATE-----
    ...
    -----END CERTIFICATE-----

internal:
  api_key: ""            # X-Internal-Key
```

---

## 7. 部署与依赖

- Auth Server 依赖：PostgreSQL、Redis
- Casdoor 作为独立服务部署（本项目不内嵌 Casdoor）
- 生产环境：所有公网接口必须走 HTTPS；内部接口建议仅内网可达

---

## 8. 开发里程碑

### Phase 1: 基础架构
- [ ] 项目骨架搭建（Gin / 配置 / 日志 / DB / Redis / Health）

### Phase 2: Casdoor 集成与业务 JWT
- [ ] Casdoor SDK 封装（换 token / 校验 / userinfo）
- [ ] `/api/v1/auth/casdoor/*` 登录回调流程
- [ ] users upsert（id=casdoor user_id）
- [ ] 业务 JWT 签发与认证中间件（不依赖 gin.Context 的 service 设计）

### Phase 3: 内部鉴权接口
- [ ] `/api/v1/internal/auth/verify`（基于用户 access_token）

### Phase 4: 订阅能力
- [ ] 套餐管理
- [ ] 订阅状态计算（`active` + `start_date/end_date`）

### Phase 5: 支付集成
- [ ] 支付宝/微信支付
- [ ] 回调签名校验 + 订单幂等
- [ ] 订阅开通/续费

### Phase 6: 测试与部署
- [ ] 单元测试（Service/Repository）
- [ ] 集成测试（关键 Handler）
- [ ] Docker/K8s 与 CI/CD

---

## 9. 安全注意事项

1. **Casdoor Token 校验**
   - 必须验证 JWT 签名（证书/公钥）
   - 必须校验 `aud`（Audience）与 `iss`
   - 必须校验 `exp/nbf` 等时效
2. **OAuth2 安全**
   - `state` 防 CSRF
   - `redirect_uri` 白名单化
3. **内部接口安全**
   - 强制校验 `X-Internal-Key`
   - 仅内网访问 + 限流 + 审计日志
4. **Access Token 安全**
   - 客户端只持有用户 access/refresh token，不再使用 API Key
   - `client_callback` 回调中 token 置于 URL query，仅建议本地回调使用
5. **敏感信息**
   - 日志禁止记录密钥/Token 明文
   - 生产配置通过环境变量注入

---

## 10. 附录

### 10.1 Go 依赖清单（示意）

```go
module github.com/Lingualink-VRChat/Lingualink_AuthServer

go 1.25
```
ay