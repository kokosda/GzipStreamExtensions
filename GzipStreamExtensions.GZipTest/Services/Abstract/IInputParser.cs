using GzipStreamExtensions.GZipTest.Facilities;

namespace GzipStreamExtensions.GZipTest.Services.Abstract
{
    public interface IInputParser
    {
        ResponseContainer<InputParserResult> Parse(string[] arguments);
    }
}
