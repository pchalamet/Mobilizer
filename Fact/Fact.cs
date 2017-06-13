using MobilizerRt;
using System;
using System.Net;

public class MainClass
{
    public static void Main()
    {
		Console.WriteLine(Fact(10));
    }

    static int Fact(int n)
    {
		Console.WriteLine("n = {0}", n);

		if (n % 2 == 0)
		{
			MobileContext.RequestUnwind(new IPEndPoint(IPAddress.Loopback, 12345));
		}
		else
		{
			MobileContext.RequestUnwind(new IPEndPoint(IPAddress.Loopback, 12346));
		}

		return n == 0 ? 1 : (n * Fact(n-1));
    }
}