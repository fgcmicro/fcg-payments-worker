using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FCGPagamentos.Application.DTOs;

public class StringDecimalConverter : JsonConverter<string>
{
    public override string Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            return reader.GetString() ?? string.Empty;
        }
        
        if (reader.TokenType == JsonTokenType.Number)
        {
            if (reader.TryGetDecimal(out var decimalValue))
            {
                return decimalValue.ToString("F2", CultureInfo.InvariantCulture);
            }
        }
        
        throw new JsonException($"Não foi possível converter o valor para string. TokenType: {reader.TokenType}");
    }

    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value);
    }
}

// DTO que corresponde exatamente ao formato enviado pela API
// Usa o mesmo namespace que a API para compatibilidade com MassTransit
// Isso permite que o MassTransit deserialize mensagens com messageType: "urn:message:FCGPagamentos.Application.DTOs:PaymentRequestedMessage"
public class PaymentRequestedMessage
{
    [JsonPropertyName("paymentId")]
    public string PaymentId { get; set; } = string.Empty;

    [JsonPropertyName("correlationId")]
    public string CorrelationId { get; set; } = string.Empty;

    [JsonPropertyName("userId")]
    public string UserId { get; set; } = string.Empty;

    [JsonPropertyName("gameId")]
    public string GameId { get; set; } = string.Empty;

    [JsonPropertyName("amount")]
    [JsonConverter(typeof(StringDecimalConverter))]
    public string Amount { get; set; } = string.Empty; // Vem como string da API

    [JsonPropertyName("currency")]
    public string Currency { get; set; } = string.Empty;

    [JsonPropertyName("paymentMethod")]
    public string PaymentMethod { get; set; } = string.Empty;

    [JsonPropertyName("occurredAt")]
    public DateTime OccurredAt { get; set; }

    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;

    public Worker.Models.PaymentRequestedMessage ToPaymentRequestedMessage()
    {
        if (!decimal.TryParse(Amount, NumberStyles.Number, CultureInfo.InvariantCulture, out var amountValue))
        {
            throw new ArgumentException($"Valor inválido para Amount: '{Amount}'. Esperado um número decimal válido.", nameof(Amount));
        }

        return new Worker.Models.PaymentRequestedMessage(
            ParseGuid(PaymentId, nameof(PaymentId)),
            ParseGuid(CorrelationId, nameof(CorrelationId)),
            ParseGuid(UserId, nameof(UserId)),
            GameId,
            amountValue,
            Currency,
            PaymentMethod,
            OccurredAt,
            Version
        );
    }

    private static Guid ParseGuid(string value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"O campo {fieldName} não pode ser nulo ou vazio.", fieldName);
        }

        if (Guid.TryParse(value, out var guid))
        {
            return guid;
        }

        // Se não for um GUID válido, gerar um GUID determinístico baseado no valor
        var bytes = System.Text.Encoding.UTF8.GetBytes(value);
        var hash = System.Security.Cryptography.SHA256.HashData(bytes);
        var guidBytes = new byte[16];
        Array.Copy(hash, 0, guidBytes, 0, 16);
        return new Guid(guidBytes);
    }
}
