//  -----------------------------------------------------------------------------
//   Filename: PaymentRequestExtensions.cs
// 
//   Description: Contains extension methods for the PaymentRequest class.:
// 
//   Author(s):
//   Donal O'Connor (donal@nofrixion.com)
// 
//   History:
//   09 06 2023  Donal O'Connor   Created, Harcourt Street,
//  Dublin, Ireland.
// 
//   License:
//   Proprietary NoFrixion.
//  -----------------------------------------------------------------------------

using NoFrixion.MoneyMoov.Models;

namespace NoFrixion.MoneyMoov.Extensions;

public static class PaymentRequestExtensions
{
    public static List<PaymentRequestPaymentAttempt> GetPaymentAttempts(this IEnumerable<PaymentRequestEvent> events, decimal amount)
    {
        if (events == null || !events.Any())
        {
            return new List<PaymentRequestPaymentAttempt>();
        }
        else
        {
            var paymentAttempts = new List<PaymentRequestPaymentAttempt>();

            paymentAttempts.AddRange(GetCardPaymentAttempts(events));
            paymentAttempts.AddRange(GetPispPaymentAttempts(events, amount));

            // TODO: Add similar logic for lightning payments.

            return paymentAttempts;
        }
    }
    
    /// <summary>
    /// Groups the payment request events into a list of payment attempts for Card payments.
    /// </summary>
    /// <returns></returns>
    public static IEnumerable<PaymentRequestPaymentAttempt> GetCardPaymentAttempts(this IEnumerable<PaymentRequestEvent> events)
    {
        var cardPaymentAttempts = new List<PaymentRequestPaymentAttempt>();

        var cardAttempts = events
            .Where(
                x => !string.IsNullOrEmpty(x.CardAuthorizationResponseID)
                     && (x.EventType == PaymentRequestEventTypesEnum.card_payer_authentication_setup
                         || x.EventType == PaymentRequestEventTypesEnum.card_authorization
                         || x.EventType == PaymentRequestEventTypesEnum.card_sale
                         || x.EventType == PaymentRequestEventTypesEnum.card_capture
                         || x.EventType == PaymentRequestEventTypesEnum.card_void)).OrderBy(x => x.Inserted)
            .GroupBy(x => x.CardAuthorizationResponseID).ToList();

        foreach (var attempt in cardAttempts)
        {
            var paymentAttempt = new PaymentRequestPaymentAttempt();
            if (attempt.Any(x => x.EventType == PaymentRequestEventTypesEnum.card_authorization))
            {
                var cardAuthorizationEvent = attempt
                    .Where(x => x.EventType == PaymentRequestEventTypesEnum.card_authorization).First();

                // The CardAuthorizationResponseID is NULL for card_payer_authentication_setup events.
                var cardAuthorizationSetupEvent = attempt.Where(
                    x => x.EventType == PaymentRequestEventTypesEnum.card_payer_authentication_setup
                         && x.CardRequestID == cardAuthorizationEvent.CardRequestID).FirstOrDefault();


                var initialEvent = cardAuthorizationSetupEvent ?? cardAuthorizationEvent;
                paymentAttempt = new PaymentRequestPaymentAttempt
                                     {
                                         AttemptKey = attempt.Key ?? string.Empty,
                                         PaymentRequestID = initialEvent.PaymentRequestID,
                                         InitiatedAt = initialEvent.Inserted,
                                         PaymentMethod = PaymentMethodTypeEnum.card,
                                         Currency = initialEvent.Currency,
                                         AttemptedAmount = cardAuthorizationEvent.Amount,
                                         PaymentProcessor = initialEvent.PaymentProcessorName
                                     };

                var isSuccessfullAuthorisationEvent =
                    cardAuthorizationEvent.Status == CardPaymentResponseStatus.CARD_AUTHORIZED_SUCCESS_STATUS
                    || cardAuthorizationEvent.Status == CardPaymentResponseStatus.CARD_PAYMENT_SOFT_DECLINE_STATUS
                    || cardAuthorizationEvent.Status == CardPaymentResponseStatus.CARD_CHECKOUT_AUTHORIZED_STATUS
                    || cardAuthorizationEvent.Status == CardPaymentResponseStatus.CARD_CHECKOUT_CARDVERFIED_STATUS;

                // If the card authorization event was successful, then the payment attempt was authorised.
                if (isSuccessfullAuthorisationEvent)
                {
                    paymentAttempt.AuthorisedAt = cardAuthorizationEvent.Inserted;
                    paymentAttempt.AuthorisedAmount = cardAuthorizationEvent.Amount;
                }
            }

            // If the card authorization event was successful and there is a card capture event, then the payment attempt was settled.
            if (attempt.Any(
                    x => x.EventType == PaymentRequestEventTypesEnum.card_sale
                         || x.EventType == PaymentRequestEventTypesEnum.card_capture))
            {
                var cardCaptureEvent =
                    attempt.Where(x => x.EventType == PaymentRequestEventTypesEnum.card_sale).FirstOrDefault()
                    ?? attempt.Where(x => x.EventType == PaymentRequestEventTypesEnum.card_capture).First();

                if (string.IsNullOrEmpty(paymentAttempt.AttemptKey))
                {
                    paymentAttempt = new PaymentRequestPaymentAttempt
                                         {
                                             AttemptKey = attempt.Key ?? string.Empty,
                                             PaymentRequestID = cardCaptureEvent.PaymentRequestID,
                                             InitiatedAt = cardCaptureEvent.Inserted,
                                             PaymentMethod = PaymentMethodTypeEnum.card,
                                             Currency = cardCaptureEvent.Currency,
                                             AttemptedAmount = cardCaptureEvent.Amount,
                                             PaymentProcessor = cardCaptureEvent.PaymentProcessorName
                                         };
                }

                var isSuccessfullCaptureEvent =
                    cardCaptureEvent.Status == CardPaymentResponseStatus.CARD_AUTHORIZED_SUCCESS_STATUS
                    || cardCaptureEvent.Status == CardPaymentResponseStatus.CARD_PAYMENT_SOFT_DECLINE_STATUS
                    || cardCaptureEvent.Status == CardPaymentResponseStatus.CARD_CHECKOUT_CAPTURED_STATUS
                    || cardCaptureEvent.Status == CardPaymentResponseStatus.CARD_CAPTURE_SUCCESS_STATUS;

                if (isSuccessfullCaptureEvent)
                {
                    paymentAttempt.SettledAt = cardCaptureEvent.Inserted;
                    paymentAttempt.SettledAmount = cardCaptureEvent.Amount;
                    if (cardCaptureEvent.PaymentProcessorName == PaymentProcessorsEnum.Checkout)
                    {
                        paymentAttempt.AuthorisedAt = cardCaptureEvent.Inserted;
                        paymentAttempt.AuthorisedAmount = cardCaptureEvent.Amount;
                    }
                }
            }

            // If there is a card void event, then the payment attempt was refunded.
            if (attempt.Any(x => x.EventType == PaymentRequestEventTypesEnum.card_void))
            {
                var cardVoidEvent = attempt.Where(x => x.EventType == PaymentRequestEventTypesEnum.card_void).First();

                if (cardVoidEvent.Status == CardPaymentResponseStatus.CARD_VOIDED_SUCCESS_STATUS)
                {
                    paymentAttempt.RefundedAt = cardVoidEvent.Inserted;
                    paymentAttempt.RefundedAmount = cardVoidEvent.Amount;
                }

            }

            if (attempt.Any(x => x.WalletName != null))
            {
                paymentAttempt.WalletName = attempt.First(x => x.WalletName != null).WalletName;
            }

            cardPaymentAttempts.Add(paymentAttempt);
        }


        return cardPaymentAttempts;
    }
    
    /// <summary>
    /// Groups the payment request events into a list of payment attempts for PISP payments.
    /// </summary>
    /// <returns></returns>
    public static IEnumerable<PaymentRequestPaymentAttempt> GetPispPaymentAttempts(this IEnumerable<PaymentRequestEvent> events, decimal amount)
    {
        var pispPaymentAttempts = new List<PaymentRequestPaymentAttempt>();
        // Get PIS attempts.
        var pispAttempts = events.Where(x => !string.IsNullOrEmpty(x.PispPaymentInitiationID) &&
                                                           ((x.EventType == PaymentRequestEventTypesEnum.pisp_initiate) ||
                                                            (x.EventType == PaymentRequestEventTypesEnum.pisp_callback) ||
                                                            (x.EventType == PaymentRequestEventTypesEnum.pisp_webhook) ||
                                                            (x.EventType == PaymentRequestEventTypesEnum.pisp_settle) ||
                                                            (x.EventType == PaymentRequestEventTypesEnum.pisp_settle_failure)))
            .OrderBy(x => x.Inserted)
            .GroupBy(x => x.PispPaymentInitiationID)
            .ToList();

        foreach (var attempt in pispAttempts)
        {
            // The pisp_initiate event should always be present but if for some reason it's not the next best event
            // will be sued as the starting point for he attempt.
            var initiateEvent =
                attempt.Where(x => x.EventType == PaymentRequestEventTypesEnum.pisp_initiate).FirstOrDefault() ??
                attempt.Where(x => x.EventType == PaymentRequestEventTypesEnum.pisp_callback).FirstOrDefault() ??
                attempt.Where(x => x.EventType == PaymentRequestEventTypesEnum.pisp_webhook).FirstOrDefault() ??
                attempt.Where(x => x.EventType == PaymentRequestEventTypesEnum.pisp_settle).FirstOrDefault();

            if (initiateEvent != null)
            {
                var paymentAttempt = new PaymentRequestPaymentAttempt
                {
                    AttemptKey = attempt.Key ?? string.Empty,
                    PaymentRequestID = initiateEvent.PaymentRequestID,
                    InitiatedAt = initiateEvent.Inserted,
                    PaymentMethod = PaymentMethodTypeEnum.pisp,
                    Currency = initiateEvent.Currency,
                    AttemptedAmount = amount,
                    PaymentProcessor = initiateEvent.PaymentProcessorName,
                    InstitutionID = initiateEvent.PispPaymentServiceProviderID
                };

                foreach (var pispCallbackOrWebhook in attempt.Where(x =>
                    x.EventType == PaymentRequestEventTypesEnum.pisp_callback ||
                    x.EventType == PaymentRequestEventTypesEnum.pisp_webhook))
                {
                    var authorisationEvent = pispCallbackOrWebhook switch
                    {
                        PaymentRequestEvent cbk when cbk.PaymentProcessorName == PaymentProcessorsEnum.Modulr
                            && cbk.Status == PaymentRequestResult.PISP_MODULR_SUCCESS_STATUS
                            && cbk.PispBankStatus != PaymentRequestResult.PISP_MODULR_BANK_REJECTED_STATUS => cbk,
                        PaymentRequestEvent cbk when cbk.PaymentProcessorName == PaymentProcessorsEnum.NoFrixion
                        && (cbk.Status == PayoutStatus.QUEUED.ToString() ||
                            cbk.Status == PayoutStatus.QUEUED_UPSTREAM.ToString() ||
                            cbk.Status == PayoutStatus.PENDING.ToString() ||
                            cbk.Status == PayoutStatus.PROCESSED.ToString()) => cbk,
                        PaymentRequestEvent cbk when cbk.PaymentProcessorName == PaymentProcessorsEnum.Plaid
                            && (cbk.Status == PaymentRequestResult.PISP_PLAID_INITIATED_STATUS ||
                                cbk.Status == PaymentRequestResult.PISP_PLAID_SUCCESS_STATUS) => cbk,
                        PaymentRequestEvent cbk when cbk.PaymentProcessorName == PaymentProcessorsEnum.Yapily
                            && (cbk.Status == PaymentRequestResult.PISP_YAPILY_PENDING_STATUS ||
                               cbk.Status == PaymentRequestResult.PISP_YAPILY_COMPLETED_STATUS) => cbk,
                        _ => null
                    };

                    if (authorisationEvent != null)
                    {
                        paymentAttempt.AuthorisedAt = authorisationEvent.Inserted;
                        paymentAttempt.AuthorisedAmount = authorisationEvent.Amount;
                        break;
                    }
                }

                if (attempt.Any(x => x.EventType == PaymentRequestEventTypesEnum.pisp_settle))
                {
                    var settleEvent = attempt.First(x => x.EventType == PaymentRequestEventTypesEnum.pisp_settle);

                    paymentAttempt.SettledAt = settleEvent.Inserted;
                    paymentAttempt.SettledAmount = settleEvent.Amount;
                }
                else if (attempt.Any(x => x.EventType == PaymentRequestEventTypesEnum.pisp_settle_failure))
                {
                    var settleFailedEvent = attempt.First(x => x.EventType == PaymentRequestEventTypesEnum.pisp_settle_failure);

                    paymentAttempt.SettleFailedAt = settleFailedEvent.Inserted;
                }

                pispPaymentAttempts.Add(paymentAttempt);
            }
        }

        return pispPaymentAttempts;
    }
}