import { OrderDetailPage } from '@/components/orders/OrderDetailPage';

type Props = { params: { id: string } };

export const metadata = {
  title: 'Detalhes do Pedido | CoreAr ERP',
};

export default function OrderDetailRoute({ params }: Props) {
  return <OrderDetailPage orderId={params.id} />;
}
