import { CurrencyPipe, DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, Signal, signal, WritableSignal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { finalize } from 'rxjs';
import { PaymentReceipt } from './shared/models/payment-receipt.model';
import { BillingService } from './core/services/billing.service';
import { OrderRequest } from './shared/models/order-request.model';

@Component({
  selector: 'app-root',
  imports: [CurrencyPipe, DatePipe, ReactiveFormsModule],
  templateUrl: './app.html',
  styleUrl: './app.less',
})
export class App {
  protected readonly loading: WritableSignal<boolean> = signal(false);
  protected readonly receipt: WritableSignal<PaymentReceipt | null> = signal<PaymentReceipt | null>(
    null,
  );
  protected readonly errorMessage: WritableSignal<string | null> = signal<string | null>(null);

  protected readonly orderForm = new FormGroup({
    orderNumber: new FormControl<string | null>(null, {
      validators: [Validators.required, Validators.maxLength(100), Validators.pattern(/\S/)],
    }),
    userId: new FormControl<string | null>(null, {
      validators: [Validators.required, Validators.maxLength(100), Validators.pattern(/\S/)],
    }),
    amount: new FormControl<number | null>(null, [
      Validators.required,
      Validators.min(0.01),
      Validators.max(999999999.99),
      Validators.pattern(/^\d+(\.\d{1,2})?$/),
    ]),
    paymentGatewayId: new FormControl('mock-success', {
      nonNullable: true,
      validators: [Validators.required],
    }),
    description: new FormControl<string | null>(null, {
      validators: [Validators.maxLength(500)],
    }),
  });

  constructor(private readonly billingService: BillingService) {}

  protected submitOrder(): void {
    if (this.loading()) {
      return;
    }

    const { orderNumber, userId, amount, paymentGatewayId, description } =
      this.orderForm.getRawValue();
    if (this.orderForm.invalid || orderNumber === null || userId === null || amount === null) {
      this.orderForm.markAllAsTouched();
      return;
    }

    this.loading.set(true);
    this.receipt.set(null);
    this.errorMessage.set(null);

    const order: OrderRequest = {
      orderNumber: orderNumber.trim(),
      userId: userId.trim(),
      amount,
      paymentGatewayId,
      description: description?.trim() || undefined,
    };

    this.billingService
      .submitOrder(order)
      .pipe(finalize((): void => this.loading.set(false)))
      .subscribe({
        next: (receipt: PaymentReceipt): void => this.receipt.set(receipt),
        error: (error: HttpErrorResponse): void => {
          if (error.error?.detail) {
            this.errorMessage.set(error.error.detail);
            return;
          }

          if (error.error?.title) {
            this.errorMessage.set(error.error.title);
            return;
          }

          this.errorMessage.set('The order could not be processed. Please try again.');
        },
      });
  }
}
