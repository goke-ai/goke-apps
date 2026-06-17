using Goke.Core.Extensions;
using System.Text;

internal class Program
{
    private static void Main(string[] args)
    {
        Console.WriteLine("Hello, World!");

        StringBuilder sb = new StringBuilder();

        sb.InsertLowerAlphabeth();

        sb.InsertUpperAlphabeth();

        Console.WriteLine(sb.ToString());

        Console.WriteLine(StringBuilder.GeneratePassword());
        Console.WriteLine(String.GeneratePassword(20));
        Console.WriteLine(String.GenerateDigits(13));
        Console.WriteLine(String.GeneratePin(13));
    }
}