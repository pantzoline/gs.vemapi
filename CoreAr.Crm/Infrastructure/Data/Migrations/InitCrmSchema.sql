-- =============================================================
-- CORE-AR: DDL - Bounded Context: CRM & Certificados
-- PostgreSQL 16+
-- =============================================================

-- ===[ CERTIFICADOS ]===
CREATE TABLE certificados (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id           UUID NOT NULL REFERENCES tenants(id),
    pedido_id           UUID NOT NULL REFERENCES pedidos(id),
    titular_nome        VARCHAR(300) NOT NULL,
    titular_cpf         CHAR(11) NOT NULL,
    tipo                VARCHAR(10) NOT NULL CHECK (tipo IN ('A1', 'A3')),
    status              certificate_status NOT NULL DEFAULT 'Pendente',
    ac_serial           VARCHAR(200),                    -- Número de série emitido pela AC
    data_emissao        DATE,
    data_expiracao      DATE,                            -- ← Índice crítico para D-30/D-15/D-7
    link_renovacao      TEXT,
    criado_em           TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    atualizado_em       TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- Índice composto CRÍTICO: varredura diária de vencimentos pelo CronWorker
-- WHERE status = 'Ativo' AND data_expiracao = CURRENT_DATE + INTERVAL 'X days'
CREATE INDEX idx_cert_expiracao_status ON certificados(status, data_expiracao)
    WHERE status = 'Ativo';

CREATE INDEX idx_cert_tenant ON certificados(tenant_id);
CREATE INDEX idx_cert_pedido ON certificados(pedido_id);

-- ===[ NOTIFICAÇÕES DE RENOVAÇÃO ]===
CREATE TABLE notificacoes_renovacao (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id       UUID NOT NULL REFERENCES tenants(id),
    certificado_id  UUID NOT NULL REFERENCES certificados(id),
    canal           VARCHAR(50) NOT NULL CHECK (canal IN ('WhatsApp', 'Email', 'SMS')),
    gatilho_dias    INT NOT NULL CHECK (gatilho_dias IN (30, 15, 7)),   -- D-30, D-15, D-7
    enviado_em      TIMESTAMPTZ,
    status          VARCHAR(50) NOT NULL DEFAULT 'Pendente',            -- Pendente | Enviado | Falhou
    criado_em       TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
CREATE INDEX idx_notif_cert ON notificacoes_renovacao(certificado_id);

-- ===[ ROW-LEVEL SECURITY ]===
ALTER TABLE certificados          ENABLE ROW LEVEL SECURITY;
ALTER TABLE notificacoes_renovacao ENABLE ROW LEVEL SECURITY;

CREATE POLICY tenant_isolation ON certificados
    USING (tenant_id = current_setting('app.tenant_id')::UUID);

CREATE POLICY tenant_isolation ON notificacoes_renovacao
    USING (tenant_id = current_setting('app.tenant_id')::UUID);

GRANT SELECT, INSERT, UPDATE ON certificados TO app_user;
GRANT SELECT, INSERT, UPDATE ON notificacoes_renovacao TO app_user;
