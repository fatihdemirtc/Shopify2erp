using System.Security.Cryptography;
using System.Text;

if (args.Length < 2)
{
    Console.Error.WriteLine("Usage: HmacComputer <json-file> <secret>");
    Console.Error.WriteLine("Example: HmacComputer requests/sample-shopify-order.json my-secret");
    return 1;
}

var filePath = args[0];
var secret = args[1];

if (!File.Exists(filePath))
{
    Console.Error.WriteLine($"File not found: {filePath}");
    return 1;
}

var payload = await File.ReadAllTextAsync(filePath);
using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
var base64 = Convert.ToBase64String(hash);

Console.WriteLine(base64);
return 0;
