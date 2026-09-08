import { zodResolver } from '@hookform/resolvers/zod';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { z } from 'zod';
import { useCurrentUser } from '../../auth/api/auth-queries';
import { archiveBankAccount, createBankAccount, createPaymentCard, removePaymentCard } from '../api/accounts-api';
import { accountKeys, useBankAccounts, usePaymentCards } from '../api/accounts-queries';

const bankAccountSchema = z.object({ bankName: z.string().trim().min(1, 'Enter the bank name.'), accountNumber: z.string().trim().min(1, 'Enter the account number.') });
const paymentCardSchema = z.object({ label: z.string().trim().min(1, 'Enter a card label.'), cardholderName: z.string().trim().min(1, 'Enter the cardholder name.'), expirationDate: z.string() });
type BankAccountValues = z.infer<typeof bankAccountSchema>;
type PaymentCardValues = z.infer<typeof paymentCardSchema>;

function formatMoney(amountMinor: number) {
  return new Intl.NumberFormat('es-ES', { style: 'currency', currency: 'EUR' }).format(amountMinor / 100);
}

export function FinancialOverviewPage() {
  const currentUser = useCurrentUser();
  const bankAccounts = useBankAccounts();
  const paymentCards = usePaymentCards();
  const queryClient = useQueryClient();
  const [showBankForm, setShowBankForm] = useState(false);
  const [showCardForm, setShowCardForm] = useState(false);
  const bankForm = useForm<BankAccountValues>({ resolver: zodResolver(bankAccountSchema), defaultValues: { bankName: '', accountNumber: '' } });
  const cardForm = useForm<PaymentCardValues>({ resolver: zodResolver(paymentCardSchema), defaultValues: { label: '', cardholderName: '', expirationDate: '' } });
  const refreshAccounts = async () => { await queryClient.invalidateQueries({ queryKey: accountKeys.all }); };
  const addBankAccount = useMutation({ mutationFn: (values: BankAccountValues) => createBankAccount({ ...values, projectId: currentUser.data?.projectId ?? '' }), onSuccess: async () => { bankForm.reset(); setShowBankForm(false); await refreshAccounts(); } });
  const addPaymentCard = useMutation({ mutationFn: (values: PaymentCardValues) => createPaymentCard({ ...values, projectId: currentUser.data?.projectId ?? '', expirationDate: values.expirationDate || undefined }), onSuccess: async () => { cardForm.reset(); setShowCardForm(false); await refreshAccounts(); } });
  const archive = useMutation({ mutationFn: ({ id, version }: { id: string; version: number }) => archiveBankAccount(id, version), onSuccess: refreshAccounts });
  const remove = useMutation({ mutationFn: ({ id, version }: { id: string; version: number }) => removePaymentCard(id, version), onSuccess: refreshAccounts });

  return <main className="page">
    <div className="page-head"><div><p className="eyebrow">Financial overview</p><h1>Accounts & cards</h1><p className="page-intro">Manage the financial sources used to record your transactions.</p></div></div>
    <section className="card" aria-labelledby="bank-accounts-title"><div className="card-head"><h2 id="bank-accounts-title">Bank accounts</h2><button type="button" className="btn btn-primary" onClick={() => setShowBankForm(true)}>Link bank account</button></div><div className="table-wrap"><table><thead><tr><th>Bank</th><th>Account</th><th className="num">Balance</th><th aria-label="Actions" /></tr></thead><tbody>{bankAccounts.data?.items.map((account) => <tr key={account.bankAccountId}><td className="cell-title">{account.bankName}</td><td>{account.accountNumber}</td><td className="num">{formatMoney(account.balanceMinor)}</td><td><div className="row-actions visible-actions"><button type="button" className="btn btn-sm" onClick={() => archive.mutate({ id: account.bankAccountId, version: account.version })}>Archive</button></div></td></tr>)}</tbody></table></div>{bankAccounts.isPending && <p className="card-body" role="status">Loading bank accounts...</p>}{bankAccounts.isError && <p className="card-body" role="alert">We could not load bank accounts.</p>}{!bankAccounts.isPending && bankAccounts.data?.items.length === 0 && <p className="empty-state">No bank accounts linked yet.</p>}</section>
    <section className="card mt-16" aria-labelledby="payment-cards-title"><div className="card-head"><h2 id="payment-cards-title">Payment cards</h2><button type="button" className="btn btn-primary" onClick={() => setShowCardForm(true)}>Add card label</button></div><div className="table-wrap"><table><thead><tr><th>Label</th><th>Cardholder</th><th>Expires</th><th aria-label="Actions" /></tr></thead><tbody>{paymentCards.data?.items.map((card) => <tr key={card.cardId}><td className="cell-title">{card.label}</td><td>{card.cardholderName}</td><td>{card.expirationDate ?? '—'}</td><td><div className="row-actions visible-actions"><button type="button" className="btn btn-sm" onClick={() => remove.mutate({ id: card.cardId, version: card.version })}>Remove</button></div></td></tr>)}</tbody></table></div>{paymentCards.isPending && <p className="card-body" role="status">Loading payment cards...</p>}{paymentCards.isError && <p className="card-body" role="alert">We could not load payment cards.</p>}{!paymentCards.isPending && paymentCards.data?.items.length === 0 && <p className="empty-state">No payment card labels yet.</p>}</section>
    {showBankForm && <div className="modal-backdrop" role="presentation"><section className="modal" role="dialog" aria-modal="true" aria-labelledby="link-account-title"><div className="modal-head"><h2 id="link-account-title">Link bank account</h2></div><form onSubmit={bankForm.handleSubmit((values) => addBankAccount.mutate(values))} noValidate><div className="modal-body form-grid"><div className="form-field full"><label htmlFor="bank-name">Bank name</label><input id="bank-name" {...bankForm.register('bankName')} />{bankForm.formState.errors.bankName && <p className="err">{bankForm.formState.errors.bankName.message}</p>}</div><div className="form-field full"><label htmlFor="account-number">Account number</label><input id="account-number" {...bankForm.register('accountNumber')} />{bankForm.formState.errors.accountNumber && <p className="err">{bankForm.formState.errors.accountNumber.message}</p>}</div>{addBankAccount.isError && <p className="form-error" role="alert">We could not link this account.</p>}</div><div className="modal-foot"><button type="button" className="btn" onClick={() => setShowBankForm(false)}>Cancel</button><button type="submit" className="btn btn-primary" disabled={addBankAccount.isPending}>Link account</button></div></form></section></div>}
    {showCardForm && <div className="modal-backdrop" role="presentation"><section className="modal" role="dialog" aria-modal="true" aria-labelledby="add-card-title"><div className="modal-head"><h2 id="add-card-title">Add payment card label</h2></div><form onSubmit={cardForm.handleSubmit((values) => addPaymentCard.mutate(values))} noValidate><div className="modal-body form-grid"><div className="form-field full"><label htmlFor="card-label">Label</label><input id="card-label" placeholder="Visa ending 1234" {...cardForm.register('label')} />{cardForm.formState.errors.label && <p className="err">{cardForm.formState.errors.label.message}</p>}</div><div className="form-field"><label htmlFor="cardholder-name">Cardholder</label><input id="cardholder-name" {...cardForm.register('cardholderName')} />{cardForm.formState.errors.cardholderName && <p className="err">{cardForm.formState.errors.cardholderName.message}</p>}</div><div className="form-field"><label htmlFor="expiration-date">Expiration date</label><input id="expiration-date" type="date" {...cardForm.register('expirationDate')} /></div>{addPaymentCard.isError && <p className="form-error full" role="alert">We could not add this card label.</p>}</div><div className="modal-foot"><button type="button" className="btn" onClick={() => setShowCardForm(false)}>Cancel</button><button type="submit" className="btn btn-primary" disabled={addPaymentCard.isPending}>Add label</button></div></form></section></div>}
  </main>;
}
