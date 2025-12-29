using FCGPagamentos.Worker.Models;

namespace fcg.Contracts;

// Contrato que corresponde ao GamePurchaseRequested publicado pelo GameService
public record GamePurchaseRequested
{
    public Guid PaymentId { get; init; }
    public string UserId { get; init; } = string.Empty;
    public string GameId { get; init; } = string.Empty;
    public double Amount { get; init; }
    public string Currency { get; init; } = string.Empty;
    public string PaymentMethod { get; init; } = string.Empty;
    public Guid CorrelationId { get; init; }

    // Método de conversão para GamePurchaseRequestedEvent
    public GamePurchaseRequestedEvent ToGamePurchaseRequestedEvent()
    {
        // Converter UserId de string para Guid
        Guid userIdGuid;
        if (!Guid.TryParse(UserId, out userIdGuid))
        {
            // Se não for um GUID válido, gerar um GUID determinístico baseado no valor
            var bytes = System.Text.Encoding.UTF8.GetBytes(UserId);
            var hash = System.Security.Cryptography.SHA256.HashData(bytes);
            var guidBytes = new byte[16];
            Array.Copy(hash, 0, guidBytes, 0, 16);
            userIdGuid = new Guid(guidBytes);
        }

        return new GamePurchaseRequestedEvent(
            PaymentId,
            userIdGuid,
            GameId,
            (decimal)Amount, // Converter double para decimal
            Currency,
            PaymentMethod,
            CorrelationId
        );
    }
}

