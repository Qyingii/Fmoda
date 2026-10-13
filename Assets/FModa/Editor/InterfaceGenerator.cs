using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using UnityEditor;
using UnityEngine;

public class InterfaceGenerator : EditorWindow
{
    private TextAsset sourceAsset;

    [MenuItem("Tools/Interface Generator")]
    public static void Open() => GetWindow<InterfaceGenerator>("接口生成器");

    private void OnGUI()
    {
        GUILayout.Label("选择要生成接口的 C# 脚本:", EditorStyles.boldLabel);
        sourceAsset = (TextAsset)EditorGUILayout.ObjectField(sourceAsset, typeof(TextAsset), false);

        if (GUILayout.Button("生成接口") && sourceAsset != null)
        {
            Generate(sourceAsset);
        }
    }

    private void Generate(TextAsset asset)
    {
        string path = AssetDatabase.GetAssetPath(asset);
        string code = File.ReadAllText(path);
        string result = GenerateInterface(code);

        if (string.IsNullOrEmpty(result))
        {
            Debug.LogError("[InterfaceGenerator] 生成失败：未找到 class 声明。");
            return;
        }

        string dir = Path.GetDirectoryName(path);
        string outName = "I" + Path.GetFileName(path);
        string outPath = Path.Combine(dir, outName).Replace("\\", "/");

        File.WriteAllText(outPath, result);
        AssetDatabase.Refresh();
        Debug.Log($"[InterfaceGenerator] 已生成: {outPath}");
    }

    private string GenerateInterface(string code)
    {
        var tree = CSharpSyntaxTree.ParseText(code);
        var root = tree.GetCompilationUnitRoot();

        var classDecl = root.DescendantNodes().OfType<ClassDeclarationSyntax>().FirstOrDefault();
        if (classDecl == null) return null;

        var ns = root.DescendantNodes().OfType<NamespaceDeclarationSyntax>().FirstOrDefault();
        string nsName = ns?.Name.ToString();

        var usings = root.Usings.Select(u => u.ToString().Trim()).Distinct().ToList();

        var sb = new StringBuilder();
        foreach (var u in usings) sb.AppendLine(u);
        sb.AppendLine();

        if (!string.IsNullOrEmpty(nsName))
        {
            sb.AppendLine($"namespace {nsName}");
            sb.AppendLine("{");
        }

        // ====== 类的注释（迁移到接口上方） ======
        AppendComments(sb, classDecl);

        string ifaceName = "I" + classDecl.Identifier.Text;
        sb.AppendLine($"    public interface {ifaceName}");
        sb.AppendLine("    {");

        foreach (var member in classDecl.Members)
        {
            switch (member)
            {
                case PropertyDeclarationSyntax prop:
                {
                    if (!IsPublicInstance(prop.Modifiers)) continue;
                    AppendComments(sb, prop);

                    string type = prop.Type.ToString();
                    string name = prop.Identifier.Text;

                    bool hasSet = prop.AccessorList?.Accessors.Any(a =>
                        a.Keyword.Text == "set" &&
                        (a.Modifiers.Count == 0 ||
                         a.Modifiers.Any(m => m.Text == "public"))
                    ) ?? false;

                    sb.AppendLine($"        {type} {name} {{ get;{(hasSet ? " set;" : "")} }}");
                    sb.AppendLine();
                    break;
                }

                case FieldDeclarationSyntax field:
                {
                    if (!IsPublicInstance(field.Modifiers)) continue;
                    if (field.Modifiers.Any(m => m.Text == "const" || m.Text == "readonly"))
                        continue;

                    AppendComments(sb, field);

                    string type = field.Declaration.Type.ToString();
                    foreach (var v in field.Declaration.Variables)
                    {
                        sb.AppendLine($"        {type} {v.Identifier.Text} {{ get; set; }}");
                    }
                    sb.AppendLine();
                    break;
                }

                case MethodDeclarationSyntax method:
                {
                    if (!IsPublicInstance(method.Modifiers)) continue;
                    if (method.Identifier.Text == classDecl.Identifier.Text) continue;

                    AppendComments(sb, method);

                    string ret = method.ReturnType.ToString();
                    string name = method.Identifier.Text;

                    string args = string.Join(", ",
                        method.ParameterList.Parameters.Select(p =>
                        {
                            string pType = p.Type?.ToString() ?? "object";
                            string pName = p.Identifier.Text;
                            string pDefault = p.Default != null ? " " + p.Default.ToString() : "";
                            string pMod = string.Join(" ", p.Modifiers.Select(m => m.Text));
                            return string.IsNullOrEmpty(pMod)
                                ? $"{pType} {pName}{pDefault}"
                                : $"{pMod} {pType} {pName}{pDefault}";
                        })
                    );

                    sb.AppendLine($"        {ret} {name}({args});");
                    sb.AppendLine();
                    break;
                }

                case EventFieldDeclarationSyntax evt:
                {
                    if (!IsPublicInstance(evt.Modifiers)) continue;
                    AppendComments(sb, evt);

                    string type = evt.Declaration.Type.ToString();
                    foreach (var v in evt.Declaration.Variables)
                    {
                        sb.AppendLine($"        event {type} {v.Identifier.Text};");
                    }
                    sb.AppendLine();
                    break;
                }
            }
        }

        sb.AppendLine("    }");
        if (!string.IsNullOrEmpty(nsName)) sb.AppendLine("}");

        return sb.ToString();
    }

    private bool IsPublicInstance(SyntaxTokenList modifiers)
    {
        if (!modifiers.Any(m => m.Text == "public")) return false;
        if (modifiers.Any(m => m.Text == "static")) return false;
        return true;
    }

    /// <summary>
    /// 完整提取成员上方的注释（//、///、/* */、/** */），
    /// 统一转成 XML doc 形式：
    ///   /// xxx
    /// 若上方没有注释，则读取行尾 //。
    /// 缩进统一为 8 空格（类注释用 4 空格，传入 indent 控制）。
    /// </summary>
    private void AppendComments(StringBuilder sb, SyntaxNode node, int indent = 8)
    {
        string pad = new string(' ', indent);
        var leading = node.GetLeadingTrivia();

        // 逐行收集纯文本内容（去掉注释符号）
        var contentLines = new List<string>();
        bool captured = false;

        foreach (var trivia in leading)
        {
            if (trivia.IsKind(SyntaxKind.SingleLineCommentTrivia))
            {
                captured = true;
                string raw = trivia.ToString();             // "// xxx"
                string text = raw.Substring(2).Trim();
                contentLines.Add(text);
            }
            else if (trivia.IsKind(SyntaxKind.MultiLineCommentTrivia))
            {
                captured = true;
                string raw = trivia.ToString();             // "/* ... */"
                string inner = raw.Substring(2, raw.Length - 4);
                foreach (var line in inner.Replace("\r\n", "\n").Split('\n'))
                {
                    string t = line.Trim();
                    if (t.StartsWith("*")) t = t.Substring(1).Trim();
                    if (!string.IsNullOrWhiteSpace(t)) contentLines.Add(t);
                }
            }
            else if (trivia.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia) ||
                     trivia.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia))
            {
                captured = true;
                string raw = trivia.ToFullString();
                foreach (var line in raw.Replace("\r\n", "\n").Split('\n'))
                {
                    string t = line.TrimStart();
                    if (t.StartsWith("///")) t = t.Substring(3);
                    else if (t.StartsWith("//")) t = t.Substring(2);
                    else if (t.StartsWith("/**")) t = t.Substring(3);
                    else if (t.StartsWith("/*")) t = t.Substring(2);
                    else if (t.StartsWith("*")) t = t.Substring(1);
                    t = t.TrimEnd();
                    if (t.EndsWith("*/")) t = t.Substring(0, t.Length - 2);
                    if (string.IsNullOrWhiteSpace(t)) continue;
                    contentLines.Add(t);
                }
            }
        }

        // 行尾注释
        if (!captured)
        {
            var trailing = node.GetTrailingTrivia()
                .FirstOrDefault(t => t.IsKind(SyntaxKind.SingleLineCommentTrivia));
            if (trailing != default)
            {
                string raw = trailing.ToString();
                string text = raw.StartsWith("//") ? raw.Substring(2).Trim() : raw.Trim();
                if (!string.IsNullOrWhiteSpace(text))
                    contentLines.Add(text);
            }
        }

        if (contentLines.Count == 0) return;

        // 判断内容里是否已经含 <summary>（避免重复包一层）
        bool hasSummary = contentLines.Any(l => l.TrimStart().StartsWith("<summary>"));

        if (!hasSummary)
        {
            sb.Append(pad).AppendLine("/// <summary>");
            foreach (var line in contentLines)
                sb.Append(pad).Append("/// ").AppendLine(line);
            sb.Append(pad).AppendLine("/// </summary>");
        }
        else
        {
            // 原样输出（内容本身就是 XML doc 结构）
            foreach (var line in contentLines)
                sb.Append(pad).Append("/// ").AppendLine(line);
        }
    }
}