'use client';

import { useForm, Controller } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { apiClient } from '@/lib/api-client';
import { AC_PROVIDERS, type CreateArFormData } from '@/types/management.types';
import { Building2, User, Mail, Phone, Hash, CheckSquare, Square, Loader2 } from 'lucide-react';
import { cn } from '@/lib/utils';

// ─── Validação CPF/CNPJ com algoritmo real ───────────────────────────────────
function validateCnpj(cnpj: string): boolean {
  const d = cnpj.replace(/\D/g, '');
  if (d.length !== 14 || /^(\d)\1+$/.test(d)) return false;

  const calc = (arr: number[], weights: number[]) =>
    arr.reduce((sum, v, i) => sum + v * weights[i], 0);

  const digits = d.split('').map(Number);
  const w1 = [5,4,3,2,9,8,7,6,5,4,3,2];
  const w2 = [6,5,4,3,2,9,8,7,6,5,4,3,2];

  const r1 = calc(digits.slice(0,12), w1) % 11;
  const d1 = r1 < 2 ? 0 : 11 - r1;
  if (d1 !== digits[12]) return false;

  const r2 = calc(digits.slice(0,13), w2) % 11;
  const d2 = r2 < 2 ? 0 : 11 - r2;
  return d2 === digits[13];
}

// ─── Schema Zod (espelha FluentValidation do backend) ────────────────────────
const createArSchema = z.object({
  name: z.string().min(3, 'Nome deve ter pelo menos 3 caracteres'),
  document: z
    .string()
    .transform(v => v.replace(/\D/g, ''))
    .refine(v => v.length === 14, 'CNPJ deve ter 14 dígitos')
    .refine(validateCnpj, 'CNPJ inválido — verifique os dígitos verificadores'),
  email: z.string().email('E-mail inválido'),
  adminName: z.string().min(3, 'Nome do administrador é obrigatório'),
  adminEmail: z.string().email('E-mail do administrador inválido'),
  enabledAcProviders: z
    .array(z.string())
    .min(1, 'Selecione pelo menos uma Autoridade Certificadora'),
});

type FormData = z.infer<typeof createArSchema>;

// ─── Campo de Input reutilizável ──────────────────────────────────────────────
function Field({
  label, error, required, icon: Icon, children
}: {
  label: string; error?: string; required?: boolean;
  icon?: React.ElementType; children: React.ReactNode;
}) {
  return (
    <div>
      <label className="block text-gray-400 text-xs font-semibold uppercase tracking-wider mb-1.5">
        {label}
        {required && <span className="text-red-400 ml-1">*</span>}
      </label>
      <div className="relative">
        {Icon && (
          <Icon className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-gray-500 pointer-events-none" />
        )}
        {children}
      </div>
      {error && (
        <p className="mt-1 text-red-400 text-xs flex items-center gap-1">
          <span>⚠</span> {error}
        </p>
      )}
    </div>
  );
}

function TextInput({
  icon, error, ...props
}: { icon?: React.ElementType; error?: boolean } & React.InputHTMLAttributes<HTMLInputElement>) {
  return (
    <input
      {...props}
      className={cn(
        'w-full py-2.5 pr-3 text-gray-200 bg-gray-800/60 border rounded-xl text-sm',
        'focus:outline-none focus:ring-1 transition-all placeholder-gray-600',
        icon ? 'pl-9' : 'pl-3',
        error
          ? 'border-red-500/60 focus:border-red-500 focus:ring-red-500/30'
          : 'border-gray-700/60 focus:border-blue-500 focus:ring-blue-500/30'
      )}
    />
  );
}

// ─── Componente Principal ─────────────────────────────────────────────────────
type CreateArFormProps = {
  onSuccess?: () => void;
  onCancel?: () => void;
};

export function CreateArForm({ onSuccess, onCancel }: CreateArFormProps) {
  const qc = useQueryClient();

  const { register, control, handleSubmit, formState: { errors } } =
    useForm<FormData>({ resolver: zodResolver(createArSchema) });

  const mutation = useMutation({
    mutationFn: (data: FormData) =>
      apiClient.post('/api/management/tenants/ar', data),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['tenant', 'hierarchy'] });
      onSuccess?.();
    },
  });

  const onSubmit = handleSubmit(data => mutation.mutate(data));

  return (
    <form onSubmit={onSubmit} className="space-y-5">
      {/* Seção: Dados da AR */}
      <div>
        <p className="text-gray-300 font-semibold text-sm mb-3 flex items-center gap-2">
          <Building2 className="w-4 h-4 text-blue-400" />
          Dados da Autoridade de Registro
        </p>
        <div className="space-y-3">
          <Field label="Nome da Empresa" error={errors.name?.message} required icon={Building2}>
            <TextInput
              {...register('name')}
              icon={Building2}
              error={!!errors.name}
              placeholder="Ex: Certificadora do Brasil Ltda."
            />
          </Field>

          <Field label="CNPJ" error={errors.document?.message} required icon={Hash}>
            <TextInput
              {...register('document')}
              icon={Hash}
              error={!!errors.document}
              placeholder="00.000.000/0001-00"
            />
          </Field>

          <Field label="E-mail da Empresa" error={errors.email?.message} required icon={Mail}>
            <TextInput
              {...register('email')}
              type="email"
              icon={Mail}
              error={!!errors.email}
              placeholder="contato@empresa.com.br"
            />
          </Field>
        </div>
      </div>

      {/* Seção: Admin */}
      <div>
        <p className="text-gray-300 font-semibold text-sm mb-3 flex items-center gap-2">
          <User className="w-4 h-4 text-emerald-400" />
          Administrador da AR
        </p>
        <div className="space-y-3">
          <Field label="Nome Completo" error={errors.adminName?.message} required icon={User}>
            <TextInput
              {...register('adminName')}
              icon={User}
              error={!!errors.adminName}
              placeholder="Nome do responsável"
            />
          </Field>
          <Field label="E-mail de Acesso" error={errors.adminEmail?.message} required icon={Mail}>
            <TextInput
              {...register('adminEmail')}
              type="email"
              icon={Mail}
              error={!!errors.adminEmail}
              placeholder="admin@empresa.com.br"
            />
          </Field>
          <p className="text-gray-600 text-xs">
            Um e-mail de boas-vindas será enviado automaticamente para definição de senha.
          </p>
        </div>
      </div>

      {/* Seção: ACs habilitadas */}
      <div>
        <p className="text-gray-300 font-semibold text-sm mb-3">
          Autoridades Certificadoras Habilitadas
        </p>
        <Controller
          name="enabledAcProviders"
          control={control}
          defaultValue={[]}
          render={({ field }) => (
            <div className="space-y-2">
              {AC_PROVIDERS.map(ac => {
                const checked = field.value.includes(ac);
                return (
                  <button
                    key={ac}
                    type="button"
                    onClick={() => {
                      const next = checked
                        ? field.value.filter((v: string) => v !== ac)
                        : [...field.value, ac];
                      field.onChange(next);
                    }}
                    className={cn(
                      'w-full flex items-center gap-3 px-4 py-3 rounded-xl',
                      'border text-sm font-medium transition-all text-left',
                      checked
                        ? 'bg-blue-600/20 border-blue-500/50 text-blue-300'
                        : 'bg-gray-800/40 border-gray-700/40 text-gray-400 hover:border-gray-600'
                    )}
                  >
                    {checked
                      ? <CheckSquare className="w-4 h-4 text-blue-400 shrink-0" />
                      : <Square className="w-4 h-4 shrink-0" />}
                    <span>{ac}</span>
                  </button>
                );
              })}
            </div>
          )}
        />
        {errors.enabledAcProviders && (
          <p className="mt-1 text-red-400 text-xs">⚠ {errors.enabledAcProviders.message}</p>
        )}
      </div>

      {/* Erro geral */}
      {mutation.isError && (
        <div className="p-3 bg-red-900/20 border border-red-700/40 rounded-xl text-red-400 text-sm">
          {(mutation.error as Error)?.message ?? 'Erro ao criar parceiro. Tente novamente.'}
        </div>
      )}

      {/* Botões */}
      <div className="flex gap-3 pt-2">
        {onCancel && (
          <button type="button" onClick={onCancel}
            className="flex-1 py-2.5 px-4 bg-gray-700/40 hover:bg-gray-700
                       text-gray-300 text-sm font-medium rounded-xl transition-all">
            Cancelar
          </button>
        )}
        <button
          type="submit"
          disabled={mutation.isPending}
          className="flex-1 flex items-center justify-center gap-2 py-2.5 px-4
                     bg-blue-600 hover:bg-blue-500 disabled:opacity-50
                     text-white text-sm font-medium rounded-xl transition-all"
        >
          {mutation.isPending && <Loader2 className="w-4 h-4 animate-spin" />}
          {mutation.isPending ? 'Criando...' : 'Criar AR'}
        </button>
      </div>
    </form>
  );
}
