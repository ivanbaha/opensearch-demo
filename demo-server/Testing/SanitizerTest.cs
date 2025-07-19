using Ganss.Xss;
using System.Text.RegularExpressions;

namespace OpenSearchDemo.Testing
{
    public static class SanitizerTest
    {
        public static void RunTest()
        {
            // Configure sanitizer to remove all tags (we only want plain text)
            var sanitizer = new HtmlSanitizer();
            sanitizer.AllowedTags.Clear();
            sanitizer.AllowedAttributes.Clear();
            sanitizer.AllowedCssProperties.Clear();
            sanitizer.AllowedSchemes.Clear();

            // Test cases with academic content
            var testCases = new[]
            {
                // JATS XML tags
                "<jats:title>Sample Title</jats:title>",
                "<jats:abstract>Sample abstract</jats:abstract>",
                
                // Common HTML tags
                "<p>Paragraph content</p>",
                "<italic>Italicized text</italic>",
                "<sup>Superscript</sup>",
                "<sub>Subscript</sub>",
                
                // Encoded entities
                "&lt;p&gt;Encoded paragraph&lt;/p&gt;",
                
                // Academic specific content
                "<ref id='ref1'>Reference 1</ref>",
                "<xref ref-type='bibr' rid='B1'>1</xref>",
                
                // Mixed content
                "Title with <italic>emphasis</italic> and <sup>2</sup> superscript",
                
                // Multiple whitespace
                "Text   with    multiple     spaces\n\nand\tlines",
                
                // Special characters
                "Text with ñoñ-ASCII characters & symbols @#$%^",
                
                // ID attributes
                "<p id='para1'>Content with ID</p>"
            };

            Console.WriteLine("Testing HtmlSanitizer behavior:");
            Console.WriteLine("================================");

            foreach (var testCase in testCases)
            {
                var sanitized = sanitizer.Sanitize(testCase);
                Console.WriteLine($"Original: {testCase}");
                Console.WriteLine($"Sanitized: {sanitized}");
                Console.WriteLine($"Same?: {testCase == sanitized}");
                Console.WriteLine();
            }
        }
    }
}
