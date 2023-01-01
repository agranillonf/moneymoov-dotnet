﻿using Newtonsoft.Json;
using System.Runtime.Serialization;

#nullable disable

namespace NoFrixion.MoneyMoov.Models
{
    public class Transaction
    {
        /// <summary>
        /// Unique ID for the transaction.
        /// </summary>
        public Guid ID { get; set; }

        /// <summary>
        /// Type of the transaction.
        /// </summary>
        public TransactionTypesEnum Type { get; set; }

        /// <summary>
        /// Amount of the transaction. Negative values indicate a pay out, positive
        ///values a pay in.
        /// </summary>
        public decimal Amount { get; set; }

        /// <summary>
        /// Currency of transaction.
        /// </summary>
        public CurrencyTypeEnum Currency { get; set; }

        /// <summary>
        /// Description of the transaction.
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Date when the transaction occurred.
        /// </summary>
        public DateTimeOffset TransactionDate { get; set; }

        /// <summary>
        /// Date when the transaction was inserted into the ledger.
        /// </summary>
        public DateTimeOffset Inserted { get; set; }

        /// <summary>
        /// Balance on the account at the completion of the transaction being processed.
        /// </summary>
        public decimal Balance { get; set; }

        /// <summary>
        /// Account of who sent the transaction
        /// </summary>
        [JsonProperty("from")]
        public AccountInfo From { get; set; }

        /// <summary>
        /// Account of who receive the transaction
        /// </summary>
        [JsonProperty("to")]
        public AccountInfo To { get; set; }
    }
}
