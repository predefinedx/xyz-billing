import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { OrderRequest } from '../../shared/models/order-request.model';
import { PaymentReceipt } from '../../shared/models/payment-receipt.model';
import { Observable } from 'rxjs';

@Injectable({ providedIn: 'root' })
export class BillingService {
  private readonly ordersUrl: string = 'http://localhost:5050/api/orders';

  constructor(private readonly http: HttpClient) {}

  submitOrder(orderRequest: OrderRequest): Observable<PaymentReceipt> {
    return this.http.post<PaymentReceipt>(this.ordersUrl, orderRequest);
  }
}
