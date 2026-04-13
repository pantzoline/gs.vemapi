# CORE-AR

> **ERP SaaS de Missão Crítica para Autoridades de Registro ICP-Brasil**  
> Gestão financeira, split de comissionamento e ciclo de vida de certificados digitais.

---

## Visão Geral

O **CORE-AR** é um sistema de gestão verticalizado para a cadeia de Autoridades de Registro (ARs) credenciadas na **Infraestrutura de Chaves Públicas Brasileira (ICP-Brasil)**. Ele resolve os três problemas centrais de uma AR:

| Problema | Solução no CORE-AR |
|---|---|
| Split de comissão entre parceiros, AGRs e ARs | Ledger de Partidas Dobradas com conciliação a Zero |
| Dupla cobrança em pagamentos | Idempotência com Redis Distributed Lock |
| Perda de receita por renovação não realizada | CronWorker D-30/D-15/D-7 com WhatsApp + Email |

---

## Sumário

- [Arquitetura](#arquitetura)
- [Estrutura do Projeto](#estrutura-do-projeto)
- [Módulos do Sistema](#módulos-do-sistema)
- [Início Rápido](#início-rápido)
- [Fluxo de Pagamento](#fluxo-de-pagamento)
- [Documentação Técnica](#documentação-técnica)
- [Comandos Úteis](#comandos-úteis)
- [Roadmap](#roadmap)

---

## Arquitetura

O sistema segue o padrão de **Monolito Modular com Event-Driven Architecture (EDA)**. Os módulos (Bounded Contexts) são isolados e se comunicam **exclusivamente via eventos de domínio** publicados no RabbitMQ. Nenhum módulo acessa diretamente o banco de dados de outro.

```
┌─────────────────────────────────────────────────────────────────┐
│                         CORE-AR SYSTEM                          │
│                                                                 │
│  ┌──────────────┐    ┌──────────────┐    ┌──────────────────┐  │
│  │   Identity   │    │   Checkout   │    │      Ledger      │  │
│  │     IAM      │───▶│  + Webhooks  │───▶│  Partidas Dobr.  │  │
│  │ RBAC / JWT   │    │  Idempotência│    │  Motor de Split  │  │
│  └──────────────┘    └──────┬───────┘    └──────────────────┘  │
│                             │ OrderPaidEvent                    │
│                    ┌────────▼────────┐                         │
│                    │    RabbitMQ     │ ◀── Barramento de Eventos│
│                    └────────┬────────┘                         │
│              ┌──────────────┼──────────────┐                   │
│              ▼              ▼              ▼                   │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐         │
│  │    Billing   │  │     CRM      │  │  Integration │         │
│  │  NF-e/NFS-e  │  │  D-30/D-15  │  │  AC Adapters │         │
│  │  Focus NFe   │  │  WhatsApp    │  │  Serasa/Cert │         │
│  └──────────────┘  └──────────────┘  └──────────────┘         │
│                                                                 │
│  ┌──────────────────────────────────────────────────────────┐  │
│  │           INFRAESTRUTURA COMPARTILHADA                   │  │
│  │  PostgreSQL 16 (RLS)  │  Redis 7  │  Docker / AWS ECS   │  │
│  └──────────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────────┘
```

> Para diagramas detalhados de fluxo de dados, modelo de domínio e camadas de segurança, consulte [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md).

---

## Estrutura do Projeto

```
gs.vemapi/
│
├── 📄 CoreAr.sln                        # Solution .NET 8 (ponto de entrada)
├── 🐳 docker-compose.yml                # Infraestrutura local (Postgres, Redis, RabbitMQ)
├── 📄 .gitignore
│
├── 📁 CoreAr.Ledger/                    # [MÓDULO C] Coração Financeiro
│   ├── Domain/
│   │   ├── Entities/
│   │   │   ├── LedgerEntry.cs           # Entidade imutável (partida dobrada)
│   │   │   └── ContratoComissionamento.cs
│   │   ├── Events/
│   │   │   └── OrderPaidEvent.cs        # Evento de domínio (record imutável)
│   │   └── Services/
│   │       └── SplitComissionamentoService.cs  # Motor de split ← NÚCLEO
│   └── Infrastructure/Data/Migrations/
│       └── InitLedgerSchema.sql         # DDL: Ledger, Pedidos, RLS, NUMERIC
│
├── 📁 CoreAr.Checkout/                  # [MÓDULO B] Motor de Pagamentos
│   ├── Api/Controllers/
│   │   └── WebhookGatewayController.cs  # Listener transaction.paid
│   ├── Application/Behaviors/
│   │   └── IdempotencyMiddleware.cs     # Redis SET NX (anti-dupla-cobrança)
│   └── Security/
│       └── WebhookSignatureValidator.cs # HMAC-SHA256 (anti-spoofing)
│
├── 📁 CoreAr.Crm/                       # [MÓDULO E] CRM e Renovação
│   ├── Workers/
│   │   ├── VencimentoCertificadoCronWorker.cs  # BackgroundService D-30/D-15/D-7
│   │   └── NotificacaoClienteConsumer.cs       # WhatsApp → fallback Email
│   └── Infrastructure/Data/Migrations/
│       └── InitCrmSchema.sql            # DDL: Certificados, índice composto
│
├── 📁 CoreAr.Billing/                   # [MÓDULO D] Motor Fiscal
│   └── Workers/
│       └── EmitirNotasFiscaisConsumer.cs  # NF-e (produto) + NFS-e (serviço)
│
├── 📁 CoreAr.Ledger.Tests/              # Testes Unitários
│   └── SplitComissionamentoTests.cs     # 8 cenários (sucesso + falhas de fraude)
│
└── 📁 docs/
    ├── ARCHITECTURE.md                  # Diagramas de fluxo e domínio detalhados
    └── TECH_STACK.md                    # Decisões técnicas fundamentadas
```

---

## Módulos do Sistema

| # | Módulo | Bounded Context | Status |
|---|---|---|---|
| A | **Identity & IAM** | Hierarquia Master → AR → AGR → Parceiro, RBAC | 🔜 Próxima fase |
| B | **Checkout & Pagamentos** | Webhooks, Idempotência Redis, HMAC-SHA256 | ✅ Implementado |
| C | **Ledger & Split** | Partidas Dobradas, Conciliação Zero, Motor de Split | ✅ Implementado |
| D | **Billing & Fiscal** | NF-e (produto) + NFS-e (serviço), Focus NFe | ✅ Implementado |
| E | **CRM & Renovação** | CronWorker D-30/D-15/D-7, WhatsApp/Email | ✅ Implementado |
| F | **Integração com ACs** | Adapters para Serasa, Certisign, Soluti | 🔜 Próxima fase |

---

## Início Rápido

### Pré-requisitos

| Ferramenta | Versão | Link |
|---|---|---|
| Docker Desktop | Qualquer | [docker.com](https://www.docker.com/products/docker-desktop/) |
| .NET SDK | **8.0+** | [dotnet.microsoft.com](https://dotnet.microsoft.com/download/dotnet/8.0) |
| Git | Qualquer | [git-scm.com](https://git-scm.com) |

### 1. Clonar o repositório

```bash
git clone <URL_DO_REPOSITORIO>
cd gs.vemapi
```

### 2. Subir a infraestrutura

```bash
docker compose up -d
```

Aguarde os healthchecks ficarem verdes. Isso sobe:

| Serviço | Porta | Acesso |
|---|---|---|
| PostgreSQL 16 | `5432` | `psql -h localhost -U corear_admin -d corear` |
| Redis 7 | `6379` | `redis-cli -h localhost -a CHANGE_ME` |
| RabbitMQ | `5672` | AMQP |
| RabbitMQ UI | `15672` | [http://localhost:15672](http://localhost:15672) (admin/CHANGE_ME) |

> ⚠️ **As migrations SQL são aplicadas automaticamente** pelo Docker na primeira inicialização.

### 3. Build & Testes

```bash
# Build completo da solution
dotnet build CoreAr.sln

# Testes unitários do Motor de Split (8 cenários)
dotnet test CoreAr.Ledger.Tests/ --logger "console;verbosity=normal"
```

### 4. Rodar a API de Checkout

```bash
dotnet run --project CoreAr.Checkout/CoreAr.Checkout.csproj
```

---

## Fluxo de Pagamento

```
Cliente
  │
  │  POST /checkout
  ▼
[CoreAr.Checkout API]
  │  valida JWT (Auth0/Keycloak)
  │  cria Pedido com idempotency_key
  │  redireciona para Gateway (Pagar.me/Stripe)
  ▼
[Gateway de Pagamento]
  │  processa cartão/pix
  │  POST /api/webhooks/gateway  {transaction.paid}
  ▼
[WebhookGatewayController]
  │  1. valida HMAC-SHA256 (anti-spoofing)
  │  2. checa Redis (anti-dupla-cobrança)
  │  3. publica OrderPaidEvent no RabbitMQ
  ▼
[RabbitMQ — Barramento de Eventos]
  ├─▶ [CoreAr.Ledger]   → lança entradas no Livro Razão (split AR/AGR/Parceiro)
  ├─▶ [CoreAr.Billing]  → emite NFS-e (serviço) + NF-e (produto) via Focus NFe
  └─▶ [CoreAr.Crm]      → registra certificado com data_expiracao
```

---

## Documentação Técnica

| Documento | Conteúdo |
|---|---|
| [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) | Bounded Contexts, modelo de domínio, camadas de segurança, RLS |
| [`docs/TECH_STACK.md`](docs/TECH_STACK.md) | Decisões técnicas fundamentadas para cada tecnologia da stack |

---

## Comandos Úteis

```bash
# Ver logs da infraestrutura
docker compose logs -f

# Parar tudo
docker compose down

# Recriar volumes (APAGA DADOS)
docker compose down -v && docker compose up -d

# Acessar banco de dados
docker exec -it corear_postgres psql -U corear_admin -d corear

# Acessar Redis CLI
docker exec -it corear_redis redis-cli -a CHANGE_ME_IN_PRODUCTION

# Rodar testes com cobertura
dotnet test CoreAr.Ledger.Tests/ --collect:"XPlat Code Coverage"
```

---

## Regras de Negócio Não Negociáveis

- 🚫 **Zero `float`/`double`** em qualquer valor monetário. Apenas `decimal` no C# e `NUMERIC(19,4)` no banco.
- 🔒 **Ledger é imutável** — `UPDATE` e `DELETE` revogados no PostgreSQL via `REVOKE`.
- ⚖️ **Todo split deve conciliar em ZERO** — créditos − débitos = 0 (validado em código e testado).
- 🔑 **Idempotência obrigatória** em toda rota de criação (`POST`) e webhook.
- 🏢 **Multitenancy via RLS** — o banco rejeita dados cross-tenant mesmo com bug no código.

---

## Roadmap

- [ ] **Módulo A** — IAM completo com RBAC e hierarquia AR/AGR/Parceiro
- [ ] **Módulo F** — Adapters para ACs (Serasa, Certisign, Soluti)
- [ ] **Frontend** — Dashboard Next.js + Tailwind + shadcn/ui
- [ ] **Autenticação** — Integração Keycloak / Auth0 com MFA obrigatório
- [ ] **Observabilidade** — OpenTelemetry + Grafana/Datadog
- [ ] **CI/CD** — GitHub Actions + deploy em AWS ECS

---

*CORE-AR — Construído para não ceder sob estresse financeiro.*
