'use client';

import { useState } from 'react';
import { PartnerHierarchy } from '@/components/management/PartnerHierarchy';
import { CreateArForm } from '@/components/management/CreateArForm';
import { CreatePaForm } from '@/components/management/CreatePaForm';
import { useTenantDetail } from '@/hooks/useTenantSettings';
import { Users, X } from 'lucide-react';
import { cn } from '@/lib/utils';
import { AC_PROVIDERS } from '@/types/management.types';

// Modal Simplificado
function Modal({ isOpen, onClose, title, children }: { isOpen: boolean; onClose: () => void; title: string; children: React.ReactNode }) {
  if (!isOpen) return null;
  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/60 backdrop-blur-sm animate-in fade-in duration-200">
      <div className="bg-gray-900 border border-gray-700/60 rounded-2xl w-full max-w-lg shadow-2xl overflow-hidden flex flex-col max-h-[90vh]">
        <div className="flex items-center justify-between p-4 border-b border-gray-800">
          <h2 className="text-lg font-bold text-white">{title}</h2>
          <button onClick={onClose} className="p-1 rounded-lg text-gray-400 hover:bg-gray-800 hover:text-white transition-all">
            <X className="w-5 h-5" />
          </button>
        </div>
        <div className="p-4 overflow-y-auto">
          {children}
        </div>
      </div>
    </div>
  );
}

export default function PartnersPage() {
  const [arModalOpen, setArModalOpen] = useState(false);
  
  const [paModalOpen, setPaModalOpen] = useState(false);
  const [selectedArId, setSelectedArId] = useState<string | null>(null);

  // Carrega detalhes da AR selecionada para pegar as ACs permitidas
  const { data: arDetail } = useTenantDetail(selectedArId);

  const startCreatePa = (arId: string) => {
    setSelectedArId(arId);
    setPaModalOpen(true);
  };

  const closePaModal = () => {
    setPaModalOpen(false);
    setTimeout(() => setSelectedArId(null), 200); // aguarda animação
  };

  return (
    <div className="max-w-screen-2xl mx-auto px-4 sm:px-6 lg:px-8 py-6 space-y-6">
      
      {/* Header */}
      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
        <div className="flex items-center gap-3">
          <div className="p-2.5 bg-blue-600/20 text-blue-400 rounded-xl">
            <Users className="w-6 h-6" />
          </div>
          <div>
            <h1 className="text-2xl font-bold text-white">Rede de Parceiros</h1>
            <p className="text-gray-400 text-sm">Gerencie a hierarquia de ARs, PAs e configurações de comissionamento.</p>
          </div>
        </div>
        <button 
          onClick={() => setArModalOpen(true)}
          className="px-4 py-2.5 bg-blue-600 hover:bg-blue-500 text-white text-sm font-medium rounded-xl transition-all shadow-lg shadow-blue-500/20"
        >
          + Nova AR
        </button>
      </div>

      {/* Árvore de Hierarquia */}
      <PartnerHierarchy 
        onCreateAr={() => setArModalOpen(true)}
        onCreatePa={startCreatePa}
      />

      {/* Modals */}
      <Modal isOpen={arModalOpen} onClose={() => setArModalOpen(false)} title="Cadastrar nova Autoridade de Registro">
        <CreateArForm 
          onCancel={() => setArModalOpen(false)} 
          onSuccess={() => setArModalOpen(false)} 
        />
      </Modal>

      <Modal isOpen={paModalOpen} onClose={closePaModal} title="Cadastrar novo Posto de Atendimento">
        {selectedArId ? (
          arDetail ? (
            <CreatePaForm 
              parentArId={selectedArId}
              parentAcProviders={arDetail.enabledAcProviders ?? [...AC_PROVIDERS]} 
              onCancel={closePaModal}
              onSuccess={closePaModal}
            />
          ) : (
            <div className="flex justify-center p-8 text-gray-500 text-sm">Carregando permissões da AR...</div>
          )
        ) : null}
      </Modal>

    </div>
  );
}
