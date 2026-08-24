// See https://aka.ms/new-console-template for more information

using XmlDocMarkdown.Core;

namespace ClassFabric.DocsGenerator;

static class Program
{
    public static int Main(string[] args)
    {
        Console.WriteLine("ClassFabric Document Generator");
        return XmlDocMarkdownApp.Run(args);
    }
}