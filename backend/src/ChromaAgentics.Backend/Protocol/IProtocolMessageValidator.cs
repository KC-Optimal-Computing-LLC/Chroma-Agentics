namespace ChromaAgentics.Backend.Protocol;

public interface IProtocolMessageValidator
{
    ProtocolValidationResult Validate(ProtocolEnvelope envelope);
}
