import { isApiError, type PlaceOrderInput, type Quote } from '@commerce/api-client';
import { Button, Container, TextField } from '@commerce/ui';
import { zodResolver } from '@hookform/resolvers/zod';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { useEffect, useRef, useState } from 'react';
import { useForm, useWatch } from 'react-hook-form';
import { Link, useNavigate } from 'react-router';
import { z } from 'zod';
import { useSession } from '@/features/auth/useSession';
import { commerce } from '@/lib/api';
import { formatTry } from '@/lib/money';

const addressSchema = z.object({
  recipient: z.string().trim().min(1, 'Enter the recipient.'),
  line1: z.string().trim().min(1, 'Enter the street address.'),
  line2: z.string().trim().optional(),
  district: z.string().trim().min(1, 'Enter the district.'),
  city: z.string().trim().min(1, 'Enter the city.'),
  postalCode: z
    .string()
    .trim()
    .regex(/^\d{5}$/, 'Enter a 5-digit postal code.'),
  country: z.string().trim().min(1, 'Enter Türkiye.'),
  notes: z.string().trim().optional(),
});

const checkoutSchema = z
  .object({
    email: z.string().trim().email('Enter a valid email address.'),
    phone: z.string().trim().min(10, 'Enter a Turkish phone number.'),
    shipping: addressSchema,
    billingSameAsShipping: z.boolean(),
    billing: addressSchema.optional(),
  })
  .superRefine((value, context) => {
    if (!value.billingSameAsShipping && !value.billing) {
      context.addIssue({ code: 'custom', path: ['billing'], message: 'Enter a billing address.' });
    }
  });

type CheckoutValues = z.infer<typeof checkoutSchema>;

export default function CheckoutPage() {
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const session = useSession();
  const idempotencyKey = useRef<string | null>(null);
  const [submitError, setSubmitError] = useState<string>();
  const [submitting, setSubmitting] = useState(false);
  const cart = useQuery({ queryKey: ['cart'], queryFn: () => commerce.cart() });
  const quote = useQuery({ queryKey: ['quote'], queryFn: () => commerce.quote() });
  const form = useForm<CheckoutValues>({
    resolver: zodResolver(checkoutSchema),
    defaultValues: {
      email: session.data?.email ?? '',
      phone: session.data?.phoneNumber ?? '',
      shipping: {
        recipient: '',
        line1: '',
        line2: '',
        district: '',
        city: '',
        postalCode: '',
        country: 'TR',
        notes: '',
      },
      billingSameAsShipping: true,
    },
  });
  const same = useWatch({ control: form.control, name: 'billingSameAsShipping' });
  useEffect(() => {
    if (session.data?.email && !form.getValues('email')) form.setValue('email', session.data.email);
    if (session.data?.phoneNumber && !form.getValues('phone'))
      form.setValue('phone', session.data.phoneNumber);
  }, [form, session.data]);

  async function place(values: CheckoutValues, current: Quote) {
    if (!cart.data) return;
    setSubmitting(true);
    setSubmitError(undefined);
    idempotencyKey.current ??= `idem-${crypto.randomUUID().replaceAll('-', '')}`;
    const shipping = cleanAddress(values.shipping);
    const input: PlaceOrderInput = {
      email: values.email,
      phone: values.phone,
      lines: cart.data.items.map((item) => ({
        variantId: item.variantId,
        quantity: item.quantity,
      })),
      shipping,
      billingSameAsShipping: values.billingSameAsShipping,
      billing:
        values.billingSameAsShipping || !values.billing ? null : cleanAddress(values.billing),
      expectedGrandTotal: current.grandTotal,
    };
    try {
      const placed = await commerce.placeOrder(idempotencyKey.current, input);
      await queryClient.invalidateQueries({ queryKey: ['cart'] });
      navigate(`/order/confirmation#${placed.accessToken}`);
    } catch (error) {
      if (isApiError(error) && error.status === 409) {
        await queryClient.invalidateQueries({ queryKey: ['quote'] });
        await queryClient.invalidateQueries({ queryKey: ['cart'] });
        setSubmitError(error.detail ?? error.title);
      } else if (isApiError(error)) {
        setSubmitError(error.detail ?? error.title);
      } else {
        setSubmitError('The order could not be placed.');
      }
    } finally {
      setSubmitting(false);
    }
  }

  if (cart.isPending || quote.isPending)
    return <Container className="py-16">Loading checkout</Container>;
  if (!cart.data || cart.data.items.length === 0) {
    return (
      <Container className="py-16">
        <h1 className="font-display text-4xl font-light">Checkout</h1>
        <p className="mt-6">Your bag is empty.</p>
        <Link to="/shop" className="mt-4 inline-flex min-h-11 items-center underline">
          Continue shopping
        </Link>
      </Container>
    );
  }

  const current = quote.data;
  return (
    <Container className="py-10">
      <h1 className="font-display text-4xl font-light">Checkout</h1>
      <p className="mt-4 max-w-prose text-sm">
        Prices and shipping are calculated by the server. Placing the order reserves stock for{' '}
        {current?.reservationExpiresInMinutes ?? 30} minutes. No payment is collected.
      </p>
      <form
        className="mt-8 grid gap-10 lg:grid-cols-[minmax(0,1fr)_20rem]"
        onSubmit={(event) => {
          void form.handleSubmit((values) => {
            if (current) void place(values, current);
          })(event);
        }}
        noValidate
      >
        <div className="flex flex-col gap-4">
          <TextField
            label="Email"
            type="email"
            autoComplete="email"
            error={form.formState.errors.email?.message}
            {...form.register('email')}
          />
          <TextField
            label="Phone"
            type="tel"
            autoComplete="tel"
            error={form.formState.errors.phone?.message}
            {...form.register('phone')}
          />
          <h2 className="mt-4 text-lg">Delivery</h2>
          <TextField
            label="Recipient"
            autoComplete="name"
            error={form.formState.errors.shipping?.recipient?.message}
            {...form.register('shipping.recipient')}
          />
          <TextField
            label="Address"
            autoComplete="address-line1"
            error={form.formState.errors.shipping?.line1?.message}
            {...form.register('shipping.line1')}
          />
          <TextField
            label="Address line 2"
            autoComplete="address-line2"
            {...form.register('shipping.line2')}
          />
          <TextField
            label="District"
            error={form.formState.errors.shipping?.district?.message}
            {...form.register('shipping.district')}
          />
          <TextField
            label="City"
            autoComplete="address-level1"
            error={form.formState.errors.shipping?.city?.message}
            {...form.register('shipping.city')}
          />
          <TextField
            label="Postal code"
            inputMode="numeric"
            autoComplete="postal-code"
            error={form.formState.errors.shipping?.postalCode?.message}
            {...form.register('shipping.postalCode')}
          />
          <TextField
            label="Country"
            autoComplete="country"
            error={form.formState.errors.shipping?.country?.message}
            {...form.register('shipping.country')}
          />
          <TextField label="Notes" {...form.register('shipping.notes')} />
          <label className="flex min-h-11 items-center gap-2 text-sm">
            <input type="checkbox" {...form.register('billingSameAsShipping')} />
            Billing address is the same as delivery
          </label>
          {same ? null : (
            <div className="flex flex-col gap-4">
              <h2 className="text-lg">Billing</h2>
              <TextField
                label="Billing recipient"
                error={form.formState.errors.billing?.recipient?.message}
                {...form.register('billing.recipient')}
              />
              <TextField
                label="Billing address"
                error={form.formState.errors.billing?.line1?.message}
                {...form.register('billing.line1')}
              />
              <TextField
                label="Billing district"
                error={form.formState.errors.billing?.district?.message}
                {...form.register('billing.district')}
              />
              <TextField
                label="Billing city"
                error={form.formState.errors.billing?.city?.message}
                {...form.register('billing.city')}
              />
              <TextField
                label="Billing postal code"
                error={form.formState.errors.billing?.postalCode?.message}
                {...form.register('billing.postalCode')}
              />
              <TextField
                label="Billing country"
                error={form.formState.errors.billing?.country?.message}
                {...form.register('billing.country')}
              />
            </div>
          )}
        </div>
        <aside className="h-fit border border-border p-4">
          {current ? <QuoteSummary quote={current} /> : <p>The quote could not be loaded.</p>}
          {submitError ? (
            <p className="mt-4" role="alert">
              {submitError}
            </p>
          ) : null}
          <Button
            className="mt-4 w-full"
            type="submit"
            loading={submitting}
            disabled={!current?.purchasable || submitting}
          >
            Place order
          </Button>
        </aside>
      </form>
    </Container>
  );
}

function QuoteSummary({ quote }: { quote: Quote }) {
  return (
    <div className="flex flex-col gap-2 text-sm">
      <p>Merchandise {formatTry(quote.merchandiseTotal)}</p>
      <p>Shipping {formatTry(quote.shippingTotal)}</p>
      <p>
        Free shipping from {formatTry(quote.freeShippingThreshold)}. Standard shipping is{' '}
        {formatTry(quote.shippingFee)}.
      </p>
      <p className="text-base">Total {formatTry(quote.grandTotal)}</p>
      <p>
        Stock is reserved for {quote.reservationExpiresInMinutes} minutes after the order is placed,
        then released if payment is still pending.
      </p>
      {quote.lines.some((line) => line.unavailableReason) ? (
        <ul>
          {quote.lines
            .filter((line) => line.unavailableReason)
            .map((line) => (
              <li key={line.variantId}>{line.unavailableReason}</li>
            ))}
        </ul>
      ) : null}
    </div>
  );
}

function cleanAddress(address: CheckoutValues['shipping']) {
  return {
    recipient: address.recipient,
    line1: address.line1,
    line2: address.line2 || null,
    district: address.district,
    city: address.city,
    postalCode: address.postalCode,
    country: address.country,
    notes: address.notes || null,
  };
}
