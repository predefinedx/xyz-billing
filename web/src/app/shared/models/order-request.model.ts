export interface OrderRequest {
  orderNumber: string;
  userId: string;
  amount: number;
  paymentGatewayId: string;
  description?: string;
}
