namespace NSA.Network.Core.Common.Interfaces;

public interface IContentBase : IRequestBase
{
    public HttpContent BuildRequest();
}
