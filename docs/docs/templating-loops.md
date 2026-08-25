# Templating Loops

TeaPie can expand a single request block into multiple independent requests, driven by a collection. This solves data-driven scenarios (seeding N entities, running the same check against a list of inputs) without copy-pasting request blocks or writing imperative loops in `.csx` scripts.

| | |
| --- | --- |
| **Syntax** | `{% for <item> in <source> %}` ... `{% endfor %}` |
| **Where** | `.http` request files and `.tp` files (in their `--- HTTP` section) |
| **Purpose** | Repeats the enclosed request block once per item in `<source>`, producing one independent request per iteration. |

## How It Fits the Pipeline

Loop expansion runs **after** the pre-request (`-init.csx`) script and **before** requests are split into individual test steps. This means:

- Any variable set in `-init.csx` via `tp.SetVariable(...)` is already available as a loop source.
- Each resulting request behaves exactly like a normal request: it gets its own name, directives (`## TEST-...`, `## AUTH-PROVIDER`, retry directives), and its own entry in the test report.
- Files that don't contain a `{% for %}` tag are left completely unchanged — templating has zero effect on ordinary request files.

## Collection Sources

Three kinds of collections can be looped over:

### 1. A Variable

The most common case: a collection set in the pre-request script.

```csharp
// 001-loop-over-partners-init.csx
tp.SetVariable("Partners", new[]
{
    new { Name = "Acme Corp", RegistrationId = "01245" },
    new { Name = "Globex Inc", RegistrationId = "012426" },
    new { Name = "Initech", RegistrationId = "012427" }
});
```

```http
{% for partner in Partners %}
### Create partner {{ forloop.index }}: {{ partner.Name }}
# @name CreatePartner{{ forloop.index }}
## TEST-EXPECT-STATUS: [201]
POST {{ApiBaseUrl}}/partners
Content-Type: application/json

{ "name": "{{ partner.Name }}", "registrationId": "{{ partner.RegistrationId }}" }
{% endfor %}
```

Any public, readable property of the item can be accessed with `{{ item.PropertyName }}`. Property names are **case-sensitive** — `{{ partner.Name }}` works, `{{ partner.name }}` resolves to an empty value.

Dotted variable names (e.g. a variable stored as `Temp.FreePartners`) work the same way: `{% for partner in Temp.FreePartners %}`.

### 2. An Inline Literal List

Useful for quick smoke tests without a pre-request script:

```http
{% for status in ("new", "used", "certified") %}
### Create listing #{{ forloop.index }} with status: {{ status }}
# @name CreateListing{{ forloop.index }}
## TEST-EXPECT-STATUS: [201]
POST {{ApiBaseUrl}}/listings
Content-Type: application/json

{ "title": "Listing {{ status }}" }
{% endfor %}
```

Supported literal types inside the parentheses, comma-separated:

- Double-quoted strings: `"new"`, `"used"`
- Booleans: `true`, `false`
- Numbers (integer or decimal): `1`, `42`, `1.5`

An empty list `()` (or whitespace-only, `(   )`) resolves to zero items, which is treated as an error (see [Guards and Error Handling](#guards-and-error-handling)).

### 3. A Numeric Range

For a fixed repeat count with no real data behind it:

```http
{% for i in (1..5) %}
### Create item {{ i }}
# @name CreateItem{{ i }}
## TEST-EXPECT-STATUS: [201]
POST {{ApiBaseUrl}}/items
Content-Type: application/json

{ "title": "Item {{ i }}", "index": {{ i }} }
{% endfor %}
```

`(1..5)` is inclusive on both ends and produces 5 iterations.

## The `forloop` Object

Inside a loop body, `{{ forloop.index }}` gives the current **1-based** iteration number. It is commonly used to build unique request names (`# @name CreateItem{{ forloop.index }}`) and unique values inside the request body.

## How Expansion Works (and What It Leaves Alone)

Loop expansion only touches `{{ }}` expressions that reference the loop variable (e.g. `partner`, `i`, `status`) or `forloop`. Every other `{{ }}` expression — TeaPie variables, functions, or named-request references — is left **untouched**, to be resolved later by TeaPie's own variable/function resolution:

```http
Input (inside the loop):
POST {{ApiBaseUrl}}/partners/{{ partner.RegistrationId }}
X-Trace-Id: {{$guid}}

After expansion (partner.RegistrationId = "01245"):
POST {{ApiBaseUrl}}/partners/01245
X-Trace-Id: {{$guid}}
```

`{{ApiBaseUrl}}` and `{{$guid}}` are resolved normally, after expansion, exactly as in a request file without any loop.

### Advanced: Referencing an Expanded Request by Name

Because request names differ per iteration (`CreatePartner1`, `CreatePartner2`, ...), a later request cannot reference a fixed name like `{{CreatePartner.response...}}`. Combine `forloop.index` with the `prepend`/`append` filters to build the reference dynamically:

```http
{% for tenant in Tenants %}
### Create company ({{ tenant.Label }})
## TEST-JSON-HAS-ID-PROPERTY: {{ forloop.index | prepend: "Temp.Attachments.CompanyId_" }}
POST {{ApiBaseUrl}}/companies

### Set license for company ({{ tenant.Label }})
POST {{ApiBaseUrl}}/companies/{{ forloop.index | prepend: "{{Temp.Attachments.CompanyId_" | append: "}}" }}/licenses
{% endfor %}
```

This pattern stores each company's id under `Temp.Attachments.CompanyId_1`, `Temp.Attachments.CompanyId_2`, etc. (for example via a custom `TEST-JSON-HAS-ID-PROPERTY` directive in a post-response script), and rebuilds `{{Temp.Attachments.CompanyId_1}}`-style references on the fly for the next request in the same iteration.

## Naming Requests Inside a Loop

If you name a request with `# @name CreatePartner`, **every** iteration produces a request with that same literal name. TeaPie logs a warning for this (naming the file, the duplicated name, and how many requests share it) but does not fail the run — only the last-registered request remains resolvable by that name. Always include `{{ forloop.index }}` (or another per-iteration value) in the name to keep it unique:

```http
# @name CreatePartner{{ forloop.index }}
```

The same warning fires for duplicate names arising between a plain request and a loop-produced one, or across multiple loops in the same file — not just within a single loop.

## Multiple Loops in One File

A single request file can contain several `{% for %}` loops, including plain (non-looped) requests between them:

```http
{% for product in Products %}
### Create product {{ forloop.index }}
# @name CreateProduct{{ forloop.index }}
POST {{ApiBaseUrl}}/products
{% endfor %}

### Plain request between the two loops
# @name MidMarker
GET {{ApiBaseUrl}}/health

{% for category in Categories %}
### Create category {{ forloop.index }}
# @name CreateCategory{{ forloop.index }}
POST {{ApiBaseUrl}}/categories
{% endfor %}
```

**Nested loops are not supported** — a `{% for %}` inside another `{% for %}` fails with an error rather than being silently misinterpreted.

## Guards and Error Handling

Templating fails loudly instead of silently producing zero or empty requests:

| Situation | Result |
| --- | --- |
| Collection variable does not exist | Error naming the missing variable and the file |
| Variable exists but is not a collection | Error stating the variable must be a collection |
| Collection resolves to zero items (empty list, `()`, or a numeric range with no items) | Error — an accidentally empty collection is almost always a mistake |
| Loop would expand to more than **1000** requests | Error, to prevent runaway expansion |
| Missing `{% endfor %}`, a stray `{% endfor %}` with no matching `{% for %}`, or malformed `{% for %}` syntax | Error identifying the malformed tag |
| Nested `{% for %}` loops | Error — not supported |
| An item property referenced in the loop body does not exist (e.g. `{{ partner.Typo }}`) | Error naming the missing member |
| Two or more requests share the same `# @name` after expansion | Warning (not an error) — see [Naming Requests Inside a Loop](#naming-requests-inside-a-loop) |

All errors include the request file's path to make them actionable.

## Inspecting the Expanded Content

The expanded requests file only ever exists in memory, so instead of writing it to disk, TeaPie logs it:

- **Debug level** — logs the content length before and after expansion (e.g. `120 -> 340`), so you can tell at a glance whether a loop actually expanded anything.
- **Trace level** — logs the **entire** expanded `.http` content for the file, exactly as it is about to be split into individual requests.

Trace is the most detailed logging level and is off by default. Enable it with:

```bash
teapie test -v                                              # verbose console output (includes Trace)
teapie test --log-file debug.log --log-file-log-level Trace # write Trace-level logs, incl. expanded content, to a file
```

See [Logging](logging.md) for all available logging levels and options.

## Current Limitations

This is a first iteration of templating support. The following are **not** supported yet:

- `{% if %}`, `{% assign %}`, `{% unless %}`, or any other Liquid/Fluid tag besides `{% for %}` / `{% endfor %}`
- Nested loops
- Templating inside `.csx` scripts (only `.http`/`.tp` request content is expanded)
- External data files (CSV/JSON) as a collection source
- Parallel execution of the requests produced by a loop
