using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using HexMaster.CodingGuidelines.Docs.Abstractions;

namespace HexMaster.CodingGuidelines.Docs.Infrastructure;

/// <summary>
/// Loads markdown documents from an embedded folder structure.
/// </summary>
public sealed class FileSystemDocumentCatalog : IDocumentCatalog
{
    private readonly string _root;
    private readonly Lazy<IReadOnlyList<DocumentInfo>> _documents;

    private static readonly Regex TitleRegex = new("^#\\s*(.+)$", RegexOptions.Multiline | RegexOptions.Compiled);

    public FileSystemDocumentCatalog(string? root = null)
    {
        _root = root ?? ResolveDefaultRoot();
        _documents = new Lazy<IReadOnlyList<DocumentInfo>>(Scan);    
    }

    private static string ResolveDefaultRoot()
    {
        // Search upwards from multiple starting points for a 'docs' folder with markdown files
        foreach (var start in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory(), Path.GetDirectoryName(typeof(FileSystemDocumentCatalog).Assembly.Location) ?? AppContext.BaseDirectory })
        {
            var candidate = FindUpForDocs(start);
            if (candidate is not null)
            {
                return candidate;
            }
        }
        // Fallback: app base/docs (may be empty)
        return Path.Combine(AppContext.BaseDirectory, "docs");
    }

    private static string? FindUpForDocs(string startDir)
    {
        var dir = new DirectoryInfo(startDir);
        while (dir is not null)
        {
            var docs = Path.Combine(dir.FullName, "docs");
            if (Directory.Exists(docs) && Directory.GetFiles(docs, "*.md", SearchOption.AllDirectories).Length > 0)
            {
                return docs;
            }
            dir = dir.Parent;
        }
        return null;
    }

    public IReadOnlyList<DocumentInfo> ListDocuments() => _documents.Value;

    public IReadOnlyList<DocumentInfo> Search(string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return Array.Empty<DocumentInfo>();
        query = query.Trim();
        var all = _documents.Value;
        return all.Where(d => d.Title.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                               File.ReadAllText(Path.Combine(_root, d.RelativePath)).Contains(query, StringComparison.OrdinalIgnoreCase))
                  .Take(50)
                  .ToList();
    }

    public async Task<string> GetContentAsync(string id, CancellationToken cancellationToken = default)
    {
        var doc = _documents.Value.FirstOrDefault(d => d.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
        if (doc is null) throw new FileNotFoundException($"Document '{id}' not found");
        var path = Path.Combine(_root, doc.RelativePath);
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync();
    }

    private IReadOnlyList<DocumentInfo> Scan()
    {
        if (!Directory.Exists(_root)) return Array.Empty<DocumentInfo>();
        var files = Directory.GetFiles(_root, "*.md", SearchOption.AllDirectories);
        var list = new List<DocumentInfo>(files.Length);
        foreach (var file in files)
        {
            var rel = Path.GetRelativePath(_root, file);
            var category = Path.GetDirectoryName(rel)?.Replace("\\", "/") ?? string.Empty;
            var content = File.ReadAllText(file);
            var title = TitleRegex.Match(content).Groups.Count > 1 ? TitleRegex.Match(content).Groups[1].Value.Trim() : Path.GetFileNameWithoutExtension(file);
            var id = GenerateId(rel);
            list.Add(new DocumentInfo(id, title, category, rel));
        }
        return list.OrderBy(d => d.Category).ThenBy(d => d.Title).ToList();
    }

    private static string GenerateId(string relativePath)
    {
        var noExt = Path.GetFileNameWithoutExtension(relativePath);
        return noExt.Replace(' ', '-').Replace('_', '-').ToLowerInvariant();
    }
}
