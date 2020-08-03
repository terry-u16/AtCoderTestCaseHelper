using System;
using System.Net;
using System.Net.Http;
using System.Collections.Generic;
using System.Linq;
using AngleSharp.Dom;
using System.Threading.Tasks;
using AngleSharp.Html.Parser;
using System.Text;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using System.IO;
using MessagePack;

namespace AtCoderTestCaseHelper
{
    class Program
    {
        readonly static HttpClient _client = new HttpClient();
        readonly static Regex _inputRegex = new Regex(@"^(?<contest>\S+?) (?<question>\S+?)$");
        readonly static Regex _inputCaseRegex = new Regex(@"<h3>入力例\s?\d</h3>");
        readonly static Regex _outputCaseRegex = new Regex(@"<h3>出力例\s?\d</h3>");

        static async Task Main(string[] args)
        {
            _client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/80.0.3987.163 Safari/537.36 Edg/80.0.361.111");
            _client.BaseAddress = new Uri("https://atcoder.jp");

            bool loggedIn = false;
            Console.WriteLine("Logging in...");
            try
            {
                var credential = await LoadCredential("credentials.bin");
                loggedIn = await LoginAsync(credential);
                Console.WriteLine($"Logged in as {credential.UserName}");
            }
            catch (IOException ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(ex.Message);
            }
            catch (NetworkConnectionException ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(ex.Message);
            }
            catch (HttpRequestException ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(ex);
            }

            if (!loggedIn)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("Login failed.");
                Console.WriteLine("You can't get some test cases.");
                Console.ResetColor();
            }

            Console.WriteLine("usage: [contestName] [questionName]");
            Console.WriteLine("example: abc162 a");

            while (true)
            {
                Console.ForegroundColor = ConsoleColor.White;
                Console.Write(">> ");
                var input = Console.ReadLine().Trim();
                Console.ResetColor();

                if (input.ToLower() == "exit")
                {
                    return;
                }

                var parsedInput = _inputRegex.Match(input);

                if (parsedInput.Success)
                {
                    try
                    {
                        var contestName = parsedInput.Groups["contest"].Value;
                        var questionName = parsedInput.Groups["question"].Value;
                        var testCases = await GetTestCasesAsync(contestName, questionName);
                        if (testCases?.Length > 0)
                        {
                            foreach (var testCase in testCases)
                            {
                                Console.ForegroundColor = ConsoleColor.DarkGray;
                                Console.WriteLine("[Input]");
                                Console.ForegroundColor = ConsoleColor.DarkGreen;
                                Console.WriteLine($"{testCase.Input}");
                                Console.ForegroundColor = ConsoleColor.DarkGray;
                                Console.WriteLine("[Output]");
                                Console.ForegroundColor = ConsoleColor.Green;
                                Console.WriteLine($"{testCase.Output}");
                            }
                            CopyToClipboard(testCases);
                            Console.ForegroundColor = ConsoleColor.Cyan;
                            Console.WriteLine("=> Copied to clipboard.");
                        }
                        else
                        {
                            Console.WriteLine("Failed to get test cases.");
                        }
                    }
                    catch (NetworkConnectionException ex)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine(ex.Message);
                    }
                    catch (HttpRequestException ex)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine(ex);
                    }
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("Invalid input.");
                }

                Console.ResetColor();
            }
        }

        const string aesKey = "+GhvQZ#Fu4CAd!P,";

        static async Task SaveCredential(string filePath)
        {
            using var aes = new AesManaged();
            var deriveBytes = new Rfc2898DeriveBytes(aesKey, 16);
            var bufferKey = deriveBytes.GetBytes(16);
            aes.Key = bufferKey;
            aes.GenerateIV();
            var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);

            using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write);
            fileStream.Write(deriveBytes.Salt);
            fileStream.Write(aes.IV);
            using var cryptoStream = new CryptoStream(fileStream, encryptor, CryptoStreamMode.Write);
            await MessagePackSerializer.SerializeAsync(cryptoStream, new LoginCredential() { UserName = "terry_u16", Password = "*********************" });
        }

        static async Task<LoginCredential> LoadCredential(string filePath)
        {
            using var fileStream = new FileStream(Path.Combine(AppDomain.CurrentDomain.BaseDirectory!, filePath), FileMode.Open, FileAccess.Read);
            var salt = new byte[16];
            fileStream.Read(salt.AsSpan());
            var iv = new byte[16];
            fileStream.Read(iv.AsSpan());

            using var aes = new AesManaged();
            aes.IV = iv;
            var deriveBytes = new Rfc2898DeriveBytes(aesKey, salt);
            var bufferKey = deriveBytes.GetBytes(16);
            aes.Key = bufferKey;

            var dectyptor = aes.CreateDecryptor(aes.Key, aes.IV);
            using var cryptoStream = new CryptoStream(fileStream, dectyptor, CryptoStreamMode.Read);
            return await MessagePackSerializer.DeserializeAsync<LoginCredential>(cryptoStream);
        }

        static async Task<bool> LoginAsync(LoginCredential credential)
        {
            var loginUri = new Uri($"login", UriKind.Relative);
            using var loginFormResult = await _client.GetAsync(loginUri);
            if (loginFormResult.IsSuccessStatusCode)
            {
                using var stream = await loginFormResult.Content.ReadAsStreamAsync();
                var parser = new HtmlParser();
                var html = await parser.ParseDocumentAsync(stream);
                var csrfTokenInput = html.QuerySelectorAll("input").First(e => e.Attributes["type"].Value == "hidden" && e.Attributes["name"].Value == "csrf_token");
                var csrfToken = WebUtility.HtmlDecode(csrfTokenInput.Attributes["value"].Value);

                var loginContent = new FormUrlEncodedContent(new Dictionary<string, string>()
                    {
                        { "username", credential.UserName ?? "" },
                        { "password", credential.Password ?? "" },
                        { "csrf_token", csrfToken }
                    });
                var loginResult = await _client.PostAsync(loginUri, loginContent);

                return loginResult.IsSuccessStatusCode && loginResult.RequestMessage.RequestUri.AbsoluteUri == "https://atcoder.jp/home";
            }
            else
            {
                throw new NetworkConnectionException($"login uriが見付かりませんでした。");
            }
        }

        static async Task<Uri> GetQuestionUriAsync(string contestName, string questionName)
        {
            var endPoint = new Uri($"contests/{contestName}/tasks", UriKind.Relative);
            using var result = await _client.GetAsync(endPoint);

            if (result.IsSuccessStatusCode)
            {
                using var stream = await result.Content.ReadAsStreamAsync();
                var parser = new HtmlParser();
                var html = await parser.ParseDocumentAsync(stream);

                var table = html.QuerySelector("table");
                var rows = table?.QuerySelectorAll("tr");
                var questions = rows?.Select(e => e.FirstElementChild.FirstElementChild)?.Select(e => new { Uri = e?.GetAttribute("href"), Symbol = e?.TextContent });
                var matchedQuestion = questions?.FirstOrDefault(q => q?.Symbol?.Equals(questionName, StringComparison.OrdinalIgnoreCase) ?? false);
                if (matchedQuestion != null && matchedQuestion.Uri != null)
                {
                    return new Uri(matchedQuestion.Uri, UriKind.Relative);
                }
                else
                {
                    throw new NetworkConnectionException($"{contestName} - {questionName}が見付かりませんでした。");
                }
            }
            else
            {
                throw new NetworkConnectionException($"{(int)result.StatusCode} {result.ReasonPhrase}");
            }
        }

        static async Task<TestCase[]> GetTestCasesAsync(string contestName, string questionName)
        {
            contestName = contestName.ToLower();
            questionName = questionName.ToLower();
            var uri = await GetQuestionUriAsync(contestName, questionName);

            using var result = await _client.GetAsync(uri);

            if (result.IsSuccessStatusCode)
            {
                var testCases = new List<TestCase>();
                using var stream = await result.Content.ReadAsStreamAsync();
                var parser = new HtmlParser();
                var doc = await parser.ParseDocumentAsync(stream);

                var inputTexts = doc.QuerySelectorAll("div.part").Where(e => _inputCaseRegex.IsMatch(e.InnerHtml)).Select(e => e.QuerySelector("pre").TextContent);
                var outputTexts = doc.QuerySelectorAll("div.part").Where(e => _outputCaseRegex.IsMatch(e.InnerHtml)).Select(e => e.QuerySelector("pre").TextContent);

                return inputTexts.Zip(outputTexts, (input, output) => new TestCase(input, output)).ToArray();
            }
            else
            {
                throw new NetworkConnectionException($"{(int)result.StatusCode} {result.ReasonPhrase}");
            }
        }

        static void CopyToClipboard(TestCase[] testCases)
        {
            var text = string.Join(Environment.NewLine, testCases.Select(t => $"[InlineData(@\"{t.Input}\", @\"{t.Output}\")]"));
            TextCopy.Clipboard.SetText($"[Theory]{Environment.NewLine}{text}");
        }
    }
}
