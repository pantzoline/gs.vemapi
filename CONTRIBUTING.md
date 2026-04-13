# Guia de Contribuição — CORE-AR

> Bem-vindo ao time! Este documento explica como o projeto está organizado e como contribuir sem quebrar nada.

---

## Antes de Começar

### Ferramentas Necessárias

| Ferramenta | Versão Mínima | Instalação |
|---|---|---|
| .NET SDK | **8.0** | [dotnet.microsoft.com](https://dotnet.microsoft.com/download/dotnet/8.0) |
| Docker Desktop | Qualquer | [docker.com](https://www.docker.com/products/docker-desktop/) |
| Git | Qualquer | [git-scm.com](https://git-scm.com) |
| VS Code ou Visual Studio | Qualquer | [code.visualstudio.com](https://code.visualstudio.com) |

**Extensões recomendadas para VS Code:**
- `ms-dotnettools.csharp` — C# IntelliSense
- `ms-azuretools.vscode-docker` — Gerenciar containers
- `bierner.markdown-mermaid` — Visualizar diagramas Mermaid nos docs

---

## Setup do Ambiente (Passo a Passo)

```bash
# 1. Clone o repositório
git clone <URL_DO_REPOSITORIO>
cd gs.vemapi

# 2. Suba a infraestrutura (Postgres + Redis + RabbitMQ)
docker compose up -d

# 3. Aguarde os healthchecks (≈ 15 segundos)
docker compose ps
# Todos os serviços devem aparecer como "healthy"

# 4. Build
dotnet build CoreAr.sln

# 5. Testes — DEVEM PASSAR antes de qualquer commit
dotnet test CoreAr.Ledger.Tests/

# 6. Rode a API de Checkout
dotnet run --project CoreAr.Checkout/CoreAr.Checkout.csproj
# Abra: http://localhost:5000 (Swagger UI)
```

---

## Estrutura de Branches

```
main          ← Código de produção. NUNCA commite direto aqui.
develop       ← Branch de integração. PRs vão para cá.
feature/XYZ   ← Nova funcionalidade (ex: feature/modulo-iam)
fix/XYZ       ← Correção de bug (ex: fix/split-arredondamento)
```

---

## Regras para Contribuir

### 1. Nenhum float/double para dinheiro

```csharp
// ❌ NUNCA
double valor = 100.00;
float  taxa  = 0.2f;

// ✅ SEMPRE
decimal valor = 100.00m;
decimal taxa  = 0.20m;
```

### 2. Nenhum módulo importa classes de outro módulo

```csharp
// ❌ PROIBIDO — acoplamento direto
using CoreAr.Ledger.Domain.Services; // em CoreAr.Billing

// ✅ CORRETO — comunicação via eventos no RabbitMQ
// CoreAr.Billing consome eventos da fila, não chama o Ledger diretamente
```

### 3. Todo PR precisa de testes

- Novas features: adicione testes em `CoreAr.Ledger.Tests/`
- Toda lógica financeira precisa de pelo menos um teste de borda (valor zero, negativo, overflow)

### 4. Nunca commite segredos

O `.gitignore` está configurado para ignorar `appsettings.Production.json`. Use:
- **Local:** `appsettings.Development.json` (ignorado pelo git, não commite)
- **Produção:** AWS Secrets Manager, variáveis de ambiente, HashiCorp Vault

### 5. Mensagens de commit em português (padrão do time)

```bash
# Formato: tipo(escopo): descrição curta
feat(ledger): adiciona validação de split negativo
fix(checkout): corrige validação HMAC para Pagar.me
docs(arch): atualiza diagrama de fluxo de eventos
test(split): adiciona cenário de arredondamento de penny
chore(deps): atualiza Npgsql para 8.0.4
```

---

## Como Testar um Webhook Localmente

Use o [ngrok](https://ngrok.com) para expor sua API local para o gateway:

```bash
# 1. Instale o ngrok: https://ngrok.com/download
# 2. Exponha a porta da API
ngrok http 5000

# 3. Copie a URL pública gerada (ex: https://abc123.ngrok.io)
# 4. Configure essa URL como webhook no painel do Pagar.me/Stripe
# 5. A assinatura HMAC será validada automaticamente
```

**Simular um webhook manualmente:**

```bash
# Substitua SEU_TENANT_ID e SUA_ASSINATURA_HMAC pelos valores reais
curl -X POST http://localhost:5000/api/webhooks/gateway \
  -H "Content-Type: application/json" \
  -H "X-Gateway-Signature: sha256=SUA_ASSINATURA_HMAC" \
  -H "X-Idempotency-Key: teste-$(date +%s)" \
  -d '{
    "tenantId": "SEU_TENANT_ID",
    "pedidoId": "00000000-0000-0000-0000-000000000001",
    "evento": "transaction.paid",
    "transactionId": "txn_teste_123",
    "valorEmCentavos": 100000
  }'
```

---

## Dúvidas Frequentes

**P: Por que usamos `NUMERIC(19,4)` e não `BIGINT` (centavos inteiros)?**  
R: O `BIGINT` exige converter em toda camada de apresentação (÷100 para exibir, ×100 para salvar). O `NUMERIC` do Postgres usa aritmética de precisão arbitrária sem ponto flutuante e divide corretamente em percentuais fracionados.

**P: Por que o Ledger é imutável?**  
R: Uma tabela com UPDATE/DELETE de saldo é um vetor de fraude e corrupção por race condition. O ledger imutável é o padrão de sistemas bancários — todo ajuste vira um novo lançamento de estorno, com trilha de auditoria completa.

**P: RabbitMQ ou SQS?**  
R: RabbitMQ para desenvolvimento local (Docker Compose). Em produção na AWS, pode ser trocado por Amazon MQ (RabbitMQ gerenciado) ou SQS sem alterar o código de domínio — as interfaces abstraem a implementação.

**P: Onde ver os logs das filas?**  
R: Acesse `http://localhost:15672` (usuário: `corear_admin`, senha: `CHANGE_ME_IN_PRODUCTION`). Lá você vê as filas, mensagens pendentes e consumers ativos em tempo real.

---

*Dúvidas? Abra uma Issue no repositório ou fale com o time de engenharia.*
