export type TenantLevel = 'Master' | 'AuthorityRegistrar' | 'PointOfAttendance' | 'Agent';

export type TenantHierarchy = {
  id: string;
  name: string;
  document: string;
  level: TenantLevel;
  isActive: boolean;
  primaryColor: string;
  logoUrl?: string;
  children: TenantHierarchy[];
  contractCount: number;
};

export type TenantDetail = {
  id: string;
  name: string;
  tradeName?: string;
  document: string;
  level: TenantLevel;
  parentId?: string;
  isActive: boolean;
  email: string;
  phone?: string;
  zipCode?: string;
  state?: string;
  city?: string;
  street?: string;
  number?: string;
  logoUrl?: string;
  primaryColor: string;
  secondaryColor: string;
  accentColor: string;
  enabledAcProviders: string[];
  userCount: number;
  contracts: PartnerContractDto[];
  createdAt: string;
};

export type PartnerContractDto = {
  id: string;
  productCode: string;
  commissionType: 'Percent' | 'FixedCents';
  commissionValue: number;
  acProvider?: string;
  isActive: boolean;
  validFrom: string;
  validUntil?: string;
};

export type TenantBranding = {
  tenantId: string;
  tenantName: string;
  logoUrl?: string;
  primaryColor: string;
  secondaryColor: string;
  accentColor: string;
};

// Metadados de UI de cada nível
export const TENANT_LEVEL_META: Record<TenantLevel, {
  label: string;
  shortLabel: string;
  color: string;
  bg: string;
  icon: string;
  depth: number;
}> = {
  Master:             { label: 'Matriz (Master)',         shortLabel: 'Master',  color: 'text-violet-400', bg: 'bg-violet-500/10', icon: '🏛️', depth: 0 },
  AuthorityRegistrar: { label: 'Autoridade de Registro', shortLabel: 'AR',      color: 'text-blue-400',   bg: 'bg-blue-500/10',   icon: '🏢', depth: 1 },
  PointOfAttendance:  { label: 'Posto de Atendimento',   shortLabel: 'PA',      color: 'text-emerald-400',bg: 'bg-emerald-500/10',icon: '🏪', depth: 2 },
  Agent:              { label: 'Agente (AGR)',            shortLabel: 'AGR',     color: 'text-amber-400',  bg: 'bg-amber-500/10',  icon: '👤', depth: 3 },
};

export type CreateArFormData = {
  name: string;
  document: string;
  email: string;
  adminName: string;
  adminEmail: string;
  enabledAcProviders: string[];
};

export type CreatePaFormData = CreateArFormData & {
  parentArId: string;
};

export const AC_PROVIDERS = ['SYNGULAR', 'VALID', 'SAFEWEB'] as const;
