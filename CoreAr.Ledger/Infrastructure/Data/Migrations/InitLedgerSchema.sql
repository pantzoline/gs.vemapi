-- =============================================================
-- CORE-AR: DDL - Bounded Context: Ledger & Pedidos
-- PostgreSQL 16+
-- REGRA: Nunca use FLOAT/DOUBLE para dinheiro.
--        Todos os valores monetários são NUMERIC(19,4).
-- =============================================================

-- ===[ MULTITENANCY: ROW-LEVEL SECURITY ]===
-- Habilitar RLS em todas as tabelas sensíveis.
-- O app_user deve ter permissão limitada (não superuser).

CREATE ROLE app_user LOGIN PASSWORD 'CHANGE_ME_IN_PRODUCTION';

-- ===[ ENUM TYPES ]===
CREATE TYPE order_status AS ENUM (
    'Pendente',
    'Aguardando_Pagamento',
    'Pago',
    'Cancelado',
    'Reembolsado',
    'Em_Disputa'
);

CREATE TYPE ledger_entry_type AS ENUM (
    'Credito',
    'Debito'
);

CREATE TYPE certificate_status AS ENUM (
    'Pendente',
    'Ativo',
    'Expirado',
    'Revogado'
);

-- ===[ TENANTS (Autoridades de Registro) ]===
CREATE TABLE tenants (
    id          UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    nome        VARCHAR(200) NOT NULL,
    cnpj        CHAR(14) NOT NULL UNIQUE,
    ativo       BOOLEAN NOT NULL DEFAULT TRUE,
    criado_em   TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- ===[ HIERARQUIA IAM: Master -> AR -> AGR -> Parceiro ]===
CREATE TABLE usuarios (
    id          UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id   UUID NOT NULL REFERENCES tenants(id),
    parent_id   UUID REFERENCES usuarios(id),        -- Hierarquia recursiva
    nome        VARCHAR(200) NOT NULL,
    email       VARCHAR(320) NOT NULL UNIQUE,
    papel       VARCHAR(50) NOT NULL,                -- 'Master','AR','AGR','Parceiro'
    ativo       BOOLEAN NOT NULL DEFAULT TRUE,
    criado_em   TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
CREATE INDEX idx_usuarios_tenant ON usuarios(tenant_id);
CREATE INDEX idx_usuarios_parent ON usuarios(parent_id);

-- ===[ PEDIDOS ]===
CREATE TABLE pedidos (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id           UUID NOT NULL REFERENCES tenants(id),
    usuario_id          UUID NOT NULL REFERENCES usuarios(id),
    gateway_order_id    VARCHAR(200),                            -- ID externo do gateway
    idempotency_key     VARCHAR(200) NOT NULL UNIQUE,           -- Chave de idempotência
    valor_total         NUMERIC(19,4) NOT NULL CHECK (valor_total > 0),
    status              order_status NOT NULL DEFAULT 'Pendente',
    criado_em           TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    atualizado_em       TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
CREATE INDEX idx_pedidos_tenant ON pedidos(tenant_id);
CREATE INDEX idx_pedidos_status ON pedidos(status);
CREATE INDEX idx_pedidos_idempotency ON pedidos(idempotency_key);

-- ===[ LEDGER: LIVRO RAZÃO IMUTÁVEL (Partidas Dobradas) ]===
-- NUNCA atualize ou delete registros desta tabela.
-- Toda correção é feita por lançamentos de estorno (reverse entry).
CREATE TABLE ledger_entries (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id       UUID NOT NULL REFERENCES tenants(id),
    pedido_id       UUID NOT NULL REFERENCES pedidos(id),
    usuario_id      UUID NOT NULL REFERENCES usuarios(id),      -- A quem pertence a partida
    tipo            ledger_entry_type NOT NULL,
    valor           NUMERIC(19,4) NOT NULL CHECK (valor > 0),   -- Sempre positivo; tipo define dir.
    descricao       VARCHAR(500) NOT NULL,
    referencia_id   UUID,                                       -- ID do grupo de lançamento (split batch)
    criado_em       TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
-- Imutabilidade via RLS + revoke UPDATE/DELETE
REVOKE UPDATE, DELETE ON ledger_entries FROM app_user;
CREATE INDEX idx_ledger_tenant ON ledger_entries(tenant_id);
CREATE INDEX idx_ledger_pedido ON ledger_entries(pedido_id);
CREATE INDEX idx_ledger_usuario ON ledger_entries(usuario_id);
CREATE INDEX idx_ledger_referencia ON ledger_entries(referencia_id);

-- ===[ ROW-LEVEL SECURITY ]===
ALTER TABLE tenants        ENABLE ROW LEVEL SECURITY;
ALTER TABLE usuarios       ENABLE ROW LEVEL SECURITY;
ALTER TABLE pedidos        ENABLE ROW LEVEL SECURITY;
ALTER TABLE ledger_entries ENABLE ROW LEVEL SECURITY;

-- Policy: o app injeta current_setting('app.tenant_id') em cada sessão.
CREATE POLICY tenant_isolation ON tenants
    USING (id = current_setting('app.tenant_id')::UUID);

CREATE POLICY tenant_isolation ON usuarios
    USING (tenant_id = current_setting('app.tenant_id')::UUID);

CREATE POLICY tenant_isolation ON pedidos
    USING (tenant_id = current_setting('app.tenant_id')::UUID);

CREATE POLICY tenant_isolation ON ledger_entries
    USING (tenant_id = current_setting('app.tenant_id')::UUID);

GRANT SELECT, INSERT ON tenants, usuarios, pedidos, ledger_entries TO app_user;
