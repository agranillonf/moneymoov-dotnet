
using System.Runtime.Serialization;
using System.Text;

namespace NoFrixion.MoneyMoov.Models.OpenBanking;

[DataContract(Name = "PayerDetails")]
public partial class PayerDetails
{
    /// <summary>
    /// __Mandatory__. The account identifications that identify the &#x60;Payer&#x60; bank account.
    /// </summary>
    /// <value>__Mandatory__. The account identifications that identify the &#x60;Payer&#x60; bank account.</value>
    [DataMember(Name = "accountIdentifications", IsRequired = true, EmitDefaultValue = true)]
    public List<AccountIdentification> AccountIdentifications { get; set; } = new List<AccountIdentification>();

    /// <summary>
    /// Returns the string presentation of the object
    /// </summary>
    /// <returns>String presentation of the object</returns>
    public override string ToString()
    {
        StringBuilder sb = new StringBuilder();
        sb.Append("class PayerDetails {\n");
        sb.Append("  AccountIdentifications: ").Append(AccountIdentifications).Append("\n");
        sb.Append("}\n");
        return sb.ToString();
    }

    /// <summary>
    /// Returns the JSON string presentation of the object
    /// </summary>
    /// <returns>JSON string presentation of the object</returns>
    public virtual string ToJson()
    {
        return Newtonsoft.Json.JsonConvert.SerializeObject(this, Newtonsoft.Json.Formatting.Indented);
    }
}
