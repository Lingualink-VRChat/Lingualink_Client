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

LinguaLink Auth Server 是 LinguaLink 生态系统的 **计费/订阅/配额** 中心服务：

- **身份认证委托 Casdoor**：用户身份（登录、OAuth、密码）由 Casdoor 统一提供
- **本服务仅管理业务数据**：订阅套餐、订单、API Key、用量记录、配额校验
- **翻译服务无状态**：Translation Server 每次请求调用 Auth Server 做配额校验与用量上报

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
│ - API Key / 订阅 / 支付 / 用量 / 配额校验     │
└───────────────┬──────────────────────────────┘
                │
   ┌────────────┴────────────┐
   │                         │
   ▼                         ▼
┌──────────┐            ┌──────────┐
│PostgreSQL│            │  Redis   │
└──────────┘            └──────────┘

Translation Server（无状态）：
  - 每次请求使用 X-Internal-Key 调 Auth Server 内部接口进行鉴权/配额校验/用量上报
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
  model/           # users/api_keys/subscriptions/orders/usage...
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
| `api_keys` | API Key（只存 hash，不存明文） |
| `subscription_plans` | 套餐定义 |
| `user_subscriptions` | 用户订阅 |
| `payment_orders` | 支付订单 |
| `usage_records` | 用量记录（按日聚合） |
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

### 3.3 api_keys - API Key 表（只存 hash）

```sql
CREATE TABLE api_keys (
    id              VARCHAR(36) PRIMARY KEY,
    user_id         VARCHAR(36) NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    name            VARCHAR(100) NOT NULL,
    prefix          VARCHAR(16) NOT NULL,      -- 用于快速定位（例如前 8~12 位）
    key_hash        VARCHAR(64) NOT NULL,      -- sha256(hex) 或更强的 KDF 结果
    status          VARCHAR(20) NOT NULL DEFAULT 'active',
    last_used_at    TIMESTAMP WITH TIME ZONE,
    created_at      TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    revoked_at      TIMESTAMP WITH TIME ZONE
);

CREATE UNIQUE INDEX uk_api_keys_prefix ON api_keys(prefix);
CREATE INDEX idx_api_keys_user ON api_keys(user_id, status);
```

> 说明：创建时仅返回一次明文 key；后续只展示 `prefix` 与元信息。

### 3.4 订阅/订单/用量表

`subscription_plans` / `user_subscriptions` / `payment_orders` / `usage_records` 用于承载订阅、订单与用量聚合统计。金额字段建议统一使用“分”为单位的整数存储，避免浮点精度问题。

---

## 4. API 接口设计

### 4.1 基础约定

- Base Path：`/api/v1`
- 用户端认证：`Authorization: Bearer <business_access_token>`
- 内部接口认证：`X-Internal-Key: <internal_api_key>`
- 响应格式：统一 `code/message/data/error`

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
| 40031 | quota_exceeded | 配额不足 |

### 4.3 接口分组

#### 4.3.1 公开接口（`/api/v1/public`）

- `GET /health`
- `GET /api/v1/public/health`
- `GET /api/v1/public/plans`（可选：公开展示套餐）

#### 4.3.2 认证接口（`/api/v1/auth`）

- `GET /api/v1/auth/casdoor/login`：跳转到 Casdoor 登录页（302 或返回 `login_url`）
- `GET /api/v1/auth/casdoor/callback`：Casdoor 回调（`code/state`），Auth Server 用 code 换 token、校验并签发业务 JWT
- `POST /api/v1/auth/refresh`：刷新业务 access_token
- `POST /api/v1/auth/logout`：注销（吊销 refresh token / 加入黑名单，策略二选一）

#### 4.3.3 用户接口（`/api/v1/user`，需要业务 JWT）

- `GET /api/v1/user/profile`：获取当前用户信息（本地 + 订阅/用量聚合）
- `PUT /api/v1/user/profile`：更新可编辑字段（如本地 `display_name`、头像自定义等）
- `GET /api/v1/user/api-keys`：列出 API Key（仅 prefix + 元信息）
- `POST /api/v1/user/api-keys`：创建 API Key（仅本次返回明文 key）
- `DELETE /api/v1/user/api-keys/:id`：吊销 API Key

#### 4.3.4 订阅接口（`/api/v1/subscription`，需要业务 JWT）

- `GET /api/v1/subscription/current`
- `GET /api/v1/subscription/usage`

#### 4.3.5 支付接口（`/api/v1/payment`，需要业务 JWT，回调例外）

- `POST /api/v1/payment/orders`
- `GET /api/v1/payment/orders/:order_id/status`
- `POST /api/v1/payment/alipay/notify`（第三方回调，无业务 JWT）
- `POST /api/v1/payment/wechat/notify`（第三方回调，无业务 JWT）

#### 4.3.6 内部接口（`/api/v1/internal`，仅服务间调用）

Translation Server 调用；必须校验 `X-Internal-Key`，并限制内网访问。

- `POST /api/v1/internal/auth/verify`：验证 API Key + 检查配额
- `POST /api/v1/internal/usage/record`：记录用量（幂等）

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
  6) 返回给 Client（JSON 或重定向携带短期 code）
```

### 5.2 配额校验与用量上报（Translation Server 集成）

```
Translation Server 收到翻译请求（包含用户 API Key）
  1) POST /api/v1/internal/auth/verify  (X-Internal-Key)
  2) 若允许 -> 执行翻译
  3) 成功后 POST /api/v1/internal/usage/record (X-Internal-Key)
```

幂等建议：`usage/record` 支持 `request_id`（由 Translation Server 生成）防止重复计费。

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

### Phase 3: API Key + 内部接口
- [ ] API Key 管理（只存 hash）
- [ ] `/api/v1/internal/auth/verify`
- [ ] `/api/v1/internal/usage/record`（幂等）

### Phase 4: 订阅与用量
- [ ] 套餐管理
- [ ] 订阅状态计算与配额模型
- [ ] 用量聚合与查询

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
4. **API Key 安全**
   - 只存 hash，不存明文
   - 生成不可预测（至少 32 bytes 随机）
   - 支持吊销与轮换
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
