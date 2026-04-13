# Stack Tecnológica — CORE-AR

> Este documento explica **o que é** cada tecnologia, **por que foi escolhida** e **como é usada** no CORE-AR. Formato ideal para onboarding de novos membros do time.

---

## Sumário

- [Backend — C# .NET 8](#backend--c-net-8)
- [Banco de Dados — PostgreSQL 16](#banco-de-dados--postgresql-16)
- [Cache e Locks — Redis 7](#cache-e-locks--redis-7)
- [Mensageria — RabbitMQ 3](#mensageria--rabbitmq-3)
- [Autenticação — Auth0 / Keycloak](#autenticação--auth0--keycloak)
- [Notas Fiscais — Focus NFe](#notas-fiscais--focus-nfe)
- [Notificações — Z-API + SendGrid](#notificações--z-api--sendgrid)
- [Infraestrutura — Docker + AWS](#infraestrutura--docker--aws)
- [Frontend (Roadmap) — Next.js + Tailwind](#frontend-roadmap--nextjs--tailwind)
- [Testes — xUnit + FluentAssertions](#testes--xunit--fluentassertions)

---

## Backend — C# .NET 8

### O que é
**.NET 8** é a plataforma de desenvolvimento da Microsoft, de código aberto e multiplataforma (Windows, Linux, macOS). O **C#** é sua linguagem principal — tipada estaticamente, compilada e com garbage collector gerenciado.

### Por que escolhemos

| Critério | Justificativa |
|---|---|
| **Tipagem estática forte** | Erros de tipo são capturados em tempo de compilação, não em produção. Crítico para sistemas financeiros. |
| **Performance** | .NET 8 compete com Go e Java em benchmarks de throughput (TechEmpower Benchmarks). |
| **Ecosystem financeiro** | Maturidade do ecossistema corporativo: MassTransit (eventos), Dapper (queries), Entity Framework Core, Polly (resiliência). |
| **Threads eficientes** | `async/await` nativo com `Task` e `ValueTask` — processamento paralelo de split e emissão de NFs sem bloqueio. |
| **Suporte a longo prazo** | .NET 8 é LTS (Long Term Support) até novembro de 2026. |

### Como usamos no CORE-AR

```
CoreAr.Checkout   → ASP.NET Core Web API (controllers, middleware)
CoreAr.Ledger     → Class Library (Domain + Services puro, sem dependência de framework)
CoreAr.Billing    → Worker Service (BackgroundService, consumers RabbitMQ)
CoreAr.Crm        → Worker Service (CronWorker agendado + consumers)
CoreAr.*.Tests    → xUnit (testes unitários isolados)
```

### Conceito chave: `decimal` para dinheiro

```csharp
// ✅ CORRETO — precisão garantida pelo compilador
decimal valorSplit = 1000.00m;

// ❌ PROIBIDO — ponto flutuante introduz imprecisão
double valorErrado = 1000.00; // pode virar 999.9999999...
float  valorErrado2 = 1000.0f; // ainda pior
```

---

## Banco de Dados — PostgreSQL 16

### O que é
**PostgreSQL** é um banco de dados relacional open-source considerado o mais avançado do mundo. Suporta transações ACID completas, tipos de dados customizados, funções e extensões poderosas.

### Por que escolhemos

| Critério | Justificativa |
|---|---|
| **ACID** | Toda mudança de status de pedido é transacional. Falha no meio → rollback automático em tudo. |
| **Row-Level Security (RLS)** | Isolamento de dados entre ARs garantido pelo próprio banco (não só pelo código). |
| **NUMERIC/DECIMAL** | Aritmética de precisão arbitrária — zero ponto flutuante para dinheiro. |
| **JSON/JSONB** | Flexibilidade para armazenar payloads de gateways e ACs sem schema rígido. |
| **Índices parciais** | `WHERE status = 'Ativo'` no índice — a query de D-30 varre apenas certificados ativos. |

### Como usamos no CORE-AR

**Tipos de dados monetários:**
```sql
-- Valores sempre NUMERIC(19,4) — nunca FLOAT, nunca DOUBLE PRECISION
valor_total    NUMERIC(19,4) NOT NULL CHECK (valor_total > 0),
valor          NUMERIC(19,4) NOT NULL CHECK (valor > 0),
```

**Row-Level Security:**
```sql
-- Aplicado em TODAS as tabelas que contêm dados de tenant
ALTER TABLE pedidos ENABLE ROW LEVEL SECURITY;

CREATE POLICY tenant_isolation ON pedidos
    USING (tenant_id = current_setting('app.tenant_id')::UUID);

-- O app injeta o tenant_id da sessão JWT em toda conexão:
SET app.tenant_id = '550e8400-e29b-41d4-a716-446655440000';
```

**Ledger imutável:**
```sql
-- O banco REJEITA updates e deletes no Ledger — mesmo de admins
REVOKE UPDATE, DELETE ON ledger_entries FROM app_user;
```

**Índice composto para vencimentos:**
```sql
-- Partial index — varre apenas Ativos, ignorando Expirados/Revogados
CREATE INDEX idx_cert_expiracao_status
    ON certificados(status, data_expiracao)
    WHERE status = 'Ativo';
```

---

## Cache e Locks — Redis 7

### O que é
**Redis** (Remote Dictionary Server) é um banco de dados em memória extremamente rápido, usado como cache, broker de mensagens e gerenciador de locks distribuídos. Opera em microssegundos.

### Por que escolhemos

| Uso | Justificativa |
|---|---|
| **Idempotência de webhooks** | `SET NX` (Set if Not eXists) é uma operação atômica — garante que apenas um processo registre a chave, mesmo com múltiplos servidores |
| **Distributed Locks** | Previne race conditions em operações críticas (ex: dois servidores processando o mesmo pedido simultaneamente) |
| **TTL automático** | A chave de idempotência expira após 24h automaticamente — sem processo de limpeza manual |

### Como usamos no CORE-AR

```csharp
// SET NX com TTL — operação 100% atômica no Redis
// Se a chave já existir, retorna false (já foi processado)
var foiAdquirido = await db.StringSetAsync(
    key:   $"idempotency:{idempotencyKey}",
    value: "processado",
    expiry: TimeSpan.FromHours(24),
    when: When.NotExists          // ← a mágica: só grava se não existir
);

if (!foiAdquirido)
    return; // Já processamos → ignorar silenciosamente
```

**Por que não usar o banco para idempotência?**  
Uma constraint `UNIQUE` no banco funcionaria, mas exigiria um `INSERT` + tratamento de exceção para cada webhook. O Redis faz o mesmo em microssegundos, sem lock de tabela.

---

## Mensageria — RabbitMQ 3

### O que é
**RabbitMQ** é um message broker open-source que implementa o protocolo AMQP. Atua como intermediário entre produtores (quem publica eventos) e consumidores (quem processa), garantindo entrega assíncrona e resiliente.

### Por que escolhemos

| Critério | Justificativa |
|---|---|
| **Desacoplamento** | O Checkout não precisa saber quem vai processar o `OrderPaidEvent`. Publica e esquece. |
| **Resiliência** | Mensagens ficam na fila mesmo se um consumer cair. Quando voltar, processa do ponto parado. |
| **Dead Letter Queue (DLQ)** | Mensagens que falham após N tentativas vão para uma fila de análise manual — sem perda. |
| **Management UI** | Interface web em `localhost:15672` para monitorar filas, mensagens e consumers em tempo real. |
| **Docker nativo** | Imagem oficial `rabbitmq:3-management-alpine` — 30MB, pronta para uso local. |

### Como usamos no CORE-AR

```
Filas configuradas:
  billing.emitir-notas           ← mensagens de faturamento
  billing.emitir-notas.dlq       ← falhas de faturamento (análise manual)
  crm.notificacoes-renovacao     ← ordens de notificação WhatsApp/Email

Fluxo de DLQ:
  Mensagem falha → BasicNack (requeue=false) → DLQ → alerta no Grafana/Datadog
```

**Configuração de DLQ:**
```csharp
_channel.QueueDeclare(
    queue: "billing.emitir-notas",
    durable: true,           // Sobrevive a restarts do RabbitMQ
    arguments: new Dictionary<string, object>
    {
        { "x-dead-letter-exchange", "" },
        { "x-dead-letter-routing-key", "billing.emitir-notas.dlq" }
    });
```

---

## Autenticação — Auth0 / Keycloak

### O que é
Plataformas de **Identity-as-a-Service (IDaaS)** que gerenciam autenticação, autorização e sessões de usuários. Implementam os padrões **OAuth 2.0** e **OpenID Connect (OIDC)**.

### Por que não criamos autenticação própria?

> *"Não crie seu próprio sistema de autenticação"* — OWASP, todo dia.

Gerenciar senhas, tokens e sessões é um domínio complexo e um vetor primário de ataques. Auth0/Keycloak já resolvem isso com:

- Armazenamento de senhas com bcrypt/Argon2 (nunca texto plano)
- MFA (Autenticação em Dois Fatores) — **obrigatório** para sistemas financeiros
- Proteção contra brute-force e credential stuffing
- Rotação de chaves RSA automática
- Logs de auditoria de acesso

### Como usamos no CORE-AR

```
Auth0 (SaaS, recomendado para inicio) ou Keycloak (self-hosted, mais controle)
    │
    │  Usuário faz login → recebe JWT (RS256)
    │
    ▼
CoreAr.Checkout API valida JWT:
  - Assinatura RSA contra JWKS público do provider
  - Claims: sub (user_id), tenant_id, papel (AR/AGR/Parceiro)
  - Expiração (exp) e audiência (aud)
    │
    ▼
Middleware injeta CurrentUser no contexto da requisição:
  - TenantId → usado para SET app.tenant_id no Postgres
  - Papel → RBAC (parceiro só vê suas próprias vendas)
```

---

## Notas Fiscais — Focus NFe

### O que é
**Focus NFe** (e alternativas como eNotas) são plataformas que abstraem a comunicação com a **SEFAZ** (Secretaria da Fazenda) de cada estado e com as **Prefeituras** para emissão de notas fiscais eletrônicas.

### Por que usamos uma plataforma e não integração direta?

Integrar diretamente com a SEFAZ envolve:
- Certificado digital A1/A3 da empresa para assinar XMLs
- Protocolo SOAP (legado) diferente para cada estado
- Schemas XML específicos por versão do NF-e
- Ambientes de homologação e produção por estado

Focus NFe abstrai tudo isso em uma REST API simples.

### Tributação híbrida de certificados digitais

Um pedido de certificado digital gera **duas notas fiscais**:

```
Pedido de Certificado A3 (R$ 600)
│
├── NFS-e (Nota Fiscal de SERVIÇO)
│   ├── Competência: Prefeitura do município da AR
│   ├── Imposto: ISS (2% a 5% dependendo do município)
│   └── Referente a: Serviço de validação de identidade
│
└── NF-e (Nota Fiscal de PRODUTO)
    ├── Competência: SEFAZ do estado da AR
    ├── Imposto: ICMS (varia por estado)
    └── Referente a: Token Smartcard físico (hardware)
```

---

## Notificações — Z-API + SendGrid

### Z-API (WhatsApp)

**Z-API** é uma API brasileira para envio de mensagens via WhatsApp Business. Alternativas: Twilio, Meta Cloud API.

**Configuração por tenant**: Cada AR pode ter seu próprio número de WhatsApp Business conectado.

### SendGrid (Email)

**SendGrid** (Twilio) é uma plataforma de envio de e-mails transacionais em escala. Alternativa: Resend.com (mais moderna).

### Estratégia de Fallback

```
Tentativa 1: WhatsApp (maior taxa de abertura)
    │
    └── Falhou (número não existe, sem internet) ?
           │
           └── Tentativa 2: Email (fallback garantido)
```

---

## Infraestrutura — Docker + AWS

### Docker Compose (Desenvolvimento Local)

Um único comando sobe todo o ambiente:
```bash
docker compose up -d  # PostgreSQL + Redis + RabbitMQ
```

Vantagem: todo desenvolvedor tem o mesmo ambiente. "Funciona na minha máquina" deixa de ser um problema.

### AWS (Produção)

| Serviço AWS | Uso |
|---|---|
| **ECS (Elastic Container Service)** | Executa os containers da aplicação |
| **RDS PostgreSQL** | Banco gerenciado com backups automáticos e failover |
| **ElastiCache Redis** | Redis gerenciado com alta disponibilidade |
| **Amazon MQ** | RabbitMQ gerenciado (ou SQS/SNS como alternativa serverless) |
| **Secrets Manager** | Armazena chaves de gateway, senhas, tokens de API |
| **CloudWatch** | Logs centralizados e alertas |

---

## Frontend (Roadmap) — Next.js + Tailwind

### O que é

- **Next.js**: Framework React com renderização híbrida (SSR/SSG/CSR), roteamento de arquivos e suporte nativo a TypeScript.
- **Tailwind CSS**: Framework CSS utility-first — estilização diretamente no HTML, sem arquivos CSS separados.
- **shadcn/ui**: Biblioteca de componentes React acessível, customizável e com design moderno.

### Por que essa combinação para um ERP?

| Problema do ERP tradicional | Solução |
|---|---|
| Interfaces feias e difíceis de usar | shadcn/ui con componentes modernos e dark mode |
| Lentidão no carregamento | Next.js SSR carrega dados no servidor antes do browser |
| Dificuldade de manutenção de CSS | Tailwind elimina CSS customizado, padroniza toda estilização |
| Falta de acessibilidade | shadcn/ui implementa WAI-ARIA em todos os componentes |

---

## Testes — xUnit + FluentAssertions

### xUnit

Framework de testes unitários padrão do ecossistema .NET. Mais moderno que NUnit e MSTest.

```csharp
[Fact]           // Teste único sem parâmetros
[Theory]         // Teste parametrizado
[InlineData(...)] // Dados do teste inline
```

### FluentAssertions

Biblioteca que torna os asserts legíveis como linguagem natural:

```csharp
// Sem FluentAssertions (difícil de ler a intenção)
Assert.Equal(0m, liquidoFinal);
Assert.Throws<ArgumentException>(() => service.CalcularSplit(contrato, ...));

// Com FluentAssertions (lê-se como especificação de negócio)
liquidoFinal.Should().Be(0m, "a conciliação do Ledger deve fechar em zero");
act.Should().Throw<ArgumentException>().WithMessage("*100%*");
```

### Cobertura de testes do Motor de Split

| Cenário | Tipo | Resultado Esperado |
|---|---|---|
| Split 70/10/20% | Sucesso | Conciliação = ZERO ✅ |
| Valores variados (R$0,03 a R$100.000) | Sucesso | Conciliação = ZERO ✅ |
| Mesmo ReferenciaId em todas as entradas | Sucesso | Rastreabilidade ✅ |
| 4 entradas geradas (1 débito + 3 créditos) | Sucesso | Estrutura correta ✅ |
| Percentuais somando 115% | Falha esperada | `ArgumentException` ✅ |
| Percentuais somando 90% | Falha esperada | `ArgumentException` ✅ |
| Percentual negativo | Falha esperada | `ArgumentException` ✅ |
| Valor do pedido zero/negativo | Falha esperada | `ArgumentException` ✅ |
