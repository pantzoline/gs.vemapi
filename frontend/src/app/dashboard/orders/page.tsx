import { OrderDataTable } from '@/components/orders/OrderDataTable';
import { FileText } from 'lucide-react';

export const metadata = {
  title: 'Pedidos | CoreAr ERP',
  description: 'Gestão de pedidos de certificados digitais',
};

export default function OrdersPage() {
  return (
    <div className="min-h-screen bg-gray-950 text-white">
      <div className="max-w-screen-2xl mx-auto px-4 sm:px-6 lg:px-8 py-6 space-y-5">
        <div className="flex items-center gap-3">
          <FileText className="w-5 h-5 text-blue-400" />
          <div>
            <h1 className="text-xl font-bold text-white">Gestão de Pedidos</h1>
            <p className="text-gray-500 text-sm">Acompanhe o ciclo de vida de cada certificado digital</p>
          </div>
        </div>
        <OrderDataTable />
      </div>
    </div>
  );
}
