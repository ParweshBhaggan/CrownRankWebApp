import fs from 'node:fs/promises'
import path from 'node:path'
import prettier from 'prettier'
import ts from 'typescript'

// Prettier handles JSX and spacing; the syntax tree identifies only function
// and class braces so object literals and JSX expressions stay intact.
async function formatFile(filename, check)
{
  const original = await fs.readFile(filename, 'utf8')
  const options = await prettier.resolveConfig(filename)
  let formatted = await prettier.format(original, { ...options, filepath: filename })
  const source = ts.createSourceFile(filename, formatted, ts.ScriptTarget.Latest, true)
  const edits = []

  function moveBrace(node)
  {
    const brace = node.getChildren(source).find(child => child.kind === ts.SyntaxKind.OpenBraceToken)
    if (!brace) return

    const position = brace.getStart(source)
    const lineStart = formatted.lastIndexOf('\n', position - 1) + 1
    const prefix = formatted.slice(lineStart, position)
    if (!prefix.trim()) return

    const indent = prefix.match(/^\s*/)[0]
    const spaces = prefix.match(/\s*$/)[0].length
    edits.push({ start: position - spaces, end: position, text: `\n${indent}` })
  }

  function visit(node)
  {
    if (ts.isClassDeclaration(node) || ts.isClassExpression(node)) moveBrace(node)
    if (ts.isFunctionLike(node) && node.body && ts.isBlock(node.body)) moveBrace(node.body)
    ts.forEachChild(node, visit)
  }

  visit(source)
  for (const edit of edits.sort((a, b) => b.start - a.start))
  {
    formatted = formatted.slice(0, edit.start) + edit.text + formatted.slice(edit.end)
  }

  if (formatted === original) return true
  if (check)
  {
    console.error(`Formatting needed: ${filename}`)
    return false
  }

  await fs.writeFile(filename, formatted)
  return true
}

async function filesIn(directory)
{
  const entries = await fs.readdir(directory, { withFileTypes: true })
  const files = []
  for (const entry of entries)
  {
    const filename = path.join(directory, entry.name)
    if (entry.isDirectory()) files.push(...await filesIn(filename))
    else if (/\.(ts|tsx)$/.test(filename)) files.push(filename)
  }
  return files
}

const check = process.argv.includes('--check')
const files = await filesIn('src')
let valid = true
for (const filename of files) valid = await formatFile(filename, check) && valid
if (!valid) process.exitCode = 1
