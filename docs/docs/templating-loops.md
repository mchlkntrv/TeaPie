# Templating Loops

TeaPie can expand a single request block into multiple independent requests, driven by a collection. This solves data-driven scenarios (seeding N entities, running the same check against a list of inputs) without copy-pasting request blocks or writing imperative loops in `.csx` scripts.

| | |
| --- | --- |
| **Syntax** | `{% for <item> in <source> %}` ... `{% endfor %}` (write the whole `{% for %}` tag on one line, with spaces around `in`) |
| **Where** | `.http` request files and `.tp` files (in their `--- HTTP` section) |
| **Purpose** | Repeats the enclosed request block once per item in `<source>`, producing one independent request per iteration. |

## How It Fits the Pipeline

Loop expansion runs **after** the pre-request (`-init.csx`) script and **before** requests are split into individual test steps. This means:

- Any variable set in `-init.csx` via `tp.SetVariable(...)` is already available as a loop source.
- Each resulting request behaves exactly like a normal request: it gets its own name, directives (`## TEST-...`, `## AUTH-PROVIDER`, retry directives), and its own entry in the test report.
- A file is treated as a template only if it contains at least one supported tag: `{% for %}`, `{% if %}`, `{% unless %}`, `{% assign %}` or `{% raw %}` (whitespace control such as `{%-` is allowed). A file using only `{% if %}`/`{% assign %}` with no loop at all is still parsed and rendered by Fluid (see [Conditions and Assignments](#conditions-and-assignments)).
- Any other file is left completely unchanged, so templating has no effect on ordinary request files. That includes a file whose body happens to contain `{%` text, such as a strftime format (`"{%Y-%m-%d}"`) or a Jinja payload. TeaPie logs a warning for such a file, in case a tag name was misspelled (tag names are case-sensitive).
- In a file that **is** a template, every `{%` is read as Fluid syntax. Wrap literal `{%` text in `{% raw %}...{% endraw %}`, for example `{"fmt": "{% raw %}{%Y-%m-%d}{% endraw %}"}`. Otherwise the file fails to parse. The same rule applies to `#` and `//` comment lines, because `# @name` lines must be templated too. A commented-out loop such as `# {% for i in (1..2) %}` therefore still runs.

## Upgrading From an Earlier Version

Templating was added in TeaPie 1.8.0. A request file written for an earlier version becomes a template as soon as it contains a supported tag, also in a body sent as data or in a comment, for example a Liquid template posted to an API:

```http
POST {{ApiBaseUrl}}/templates
Content-Type: application/json

{"template": "{% if customer %}Hello {{ customer.name }}{% endif %}"}
```

After the upgrade, such a file is rendered before it is sent. An undefined name in `{% if %}` is falsy, so this request is sent as `{"template": ""}`, without an error or a warning. Other such files fail with a templating error that stops the run, for example a comment with an `{% if %}` that has no `{% endif %}`. To send the text unchanged, wrap it in `{% raw %}...{% endraw %}`:

```http
{"template": "{% raw %}{% if customer %}Hello {{ customer.name }}{% endif %}{% endraw %}"}
```

Before upgrading, search the collection for `{%` followed by `for`, `if`, `unless`, `assign` or `raw` (also as `{%-`).

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

Any public, readable property of the item can be accessed with `{{ item.PropertyName }}`. Property names are **case-sensitive**: `{{ partner.Name }}` works, while `{{ partner.name }}` is an error naming the missing member and its first use. This holds for every item that is a typed object (an anonymous type, a record or a class), in `{% if %}` conditions too. For an item that is a dictionary or JSON (a `Dictionary<string, object>`, an item of a `JArray`, or an item read with `GetBodyAsExpando()`), a missing key gives an empty value instead, which is falsy in a condition.

Items of a JSON body read with `GetBodyAsExpando()` (e.g. `tp.SetVariable("Customers", body.items)` with `dynamic body`) can be looped over directly. Their property names are not case-sensitive, the same as in scripts.

Dotted variable names (e.g. a variable stored as `Temp.FreePartners`) work the same way as a loop source: `{% for partner in Temp.FreePartners %}`. Elsewhere in a template they can't be read — see [Conditions and Assignments](#conditions-and-assignments).

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

`(1..5)` is inclusive on both ends and produces 5 iterations. Both bounds must be whole numbers from 0 written directly in the file. A variable (`(1..Count)`), an expression, a negative or a decimal number as a bound is an error.

## Loop Options

A collection can be followed by options, separated by spaces, in any order:

| Option | Meaning |
| --- | --- |
| `limit: n` | Iterate over at most `n` items (standard Liquid) |
| `offset: n` | Skip the first `n` items (standard Liquid) |
| `reversed` | Iterate in reverse order, applied after `offset`/`limit` (standard Liquid) |

```http
{% for partner in Partners offset: 1 limit: 10 reversed %}
...
{% endfor %}
```

The value of `limit:`/`offset:` must be a whole number from 0 to 1000, written directly or held in a variable (`limit: MaxPartners`). Anything else is an error naming the file: a negative or decimal number, text, or Liquid's `offset: continue`. A number written directly is checked before rendering, a variable when its loop starts. The 1000-request guard below counts the requests a loop actually generates, so `limit:` and `{% if %}` conditions are taken into account.

A loop source does **not** accept filters — `{% for x in Items | sort %}` is an error, because Liquid's `for` tag only takes a collection followed by the options above.

## The `forloop` Object

Inside a loop body, `{{ forloop.index }}` gives the current **1-based** iteration number. It is commonly used to build unique request names (`# @name CreateItem{{ forloop.index }}`) and unique values inside the request body.

The fields you'll use most often:

| Field | Meaning |
| --- | --- |
| `forloop.index` | 1-based iteration number |
| `forloop.index0` | 0-based iteration number |
| `forloop.first` | `true` on the first iteration, `false` otherwise |
| `forloop.last` | `true` on the last iteration, `false` otherwise |

All four are usable anywhere inside the loop body, including inside `# @name` declarations. `forloop` is Fluid's native loop object, so its other standard fields (`forloop.length`, `forloop.rindex`, `forloop.rindex0`) are also available, even though TeaPie doesn't add anything on top of them.
Expansion happens **before** request names are parsed, so `# @name Create{{ forloop.index }}`
becomes a literal name like `# @name Create1` before TeaPie's own request parser ever sees it —
the parser and everything downstream of it (including `tp.Responses[...]` lookups and the test
report) only ever see the rendered literal name.

## How Expansion Works (and What It Leaves Alone)

Loop expansion only touches `{{ }}` expressions that reference the loop variable (e.g. `partner`, `i`, `status`), `forloop` or a name set by `{% assign %}` — anywhere in the expression, so `{{ "p-" | append: partner.Id }}` and `{{ 1 | plus: forloop.index }}` are rendered too. Such an expression is rendered as a whole by Fluid, so any other name in it is read from TeaPie's variables. A name that is undefined, tagged as `secret` or dotted (such as `Temp.Id`) renders as an empty value, without an error or a warning — `{{ "p-" | append: partner.Id | append: Typo }}` gives `p-7`. Every other `{{ }}` expression — TeaPie variables, functions, or named-request references — is left **untouched**, to be resolved later by TeaPie's own variable/function resolution:

```http
Input (inside the loop):
POST {{ApiBaseUrl}}/partners/{{ partner.RegistrationId }}
X-Trace-Id: {{$guid}}

After expansion (partner.RegistrationId = "01245"):
POST {{ApiBaseUrl}}/partners/01245
X-Trace-Id: {{$guid}}
```

`{{ApiBaseUrl}}` and `{{$guid}}` are resolved normally, after expansion, exactly as in a request file without any loop.

### Values From Data Stay Data

Expansion runs before the file is split into requests and before TeaPie resolves its variables. A rendered value that contains a line break or `###` would therefore change the file itself — add a request or a header, or cut a request short — and a value with `{{` or `}}` would pull in another variable, a secret one included. Such a value is not written into the file. The expansion writes a reference such as `{{__teapie_value_1}}` instead, and TeaPie puts the value in place of it when it resolves the request's variables, exactly as it does with `{{variable}}` in a file without templates:

```http
Input (inside the loop, partner.Note = "first line\nsecond line"):
POST {{ApiBaseUrl}}/partners/{{ partner.Id }}/notes
Content-Type: text/plain

{{ partner.Note }}

After expansion:
POST {{ApiBaseUrl}}/partners/7/notes
Content-Type: text/plain

{{__teapie_value_1}}
```

The request is sent with both lines of the note in its body. A value such as `{{apiKey}}` coming from data is sent as this literal text, not as the value of `apiKey`.

- The check applies to the result of each `{{ }}` expression, after its filters. Filters and conditions work with the real value, so `{{ partner.Note | json }}` writes `"first line\nsecond line"` into a JSON body as it did before, and `| url_encode` or `| strip_newlines` leave nothing to replace.
- `{{` and `}}` are written into the file only when they are written as text in that same expression, as in the pattern in the next section. Text that only reaches the expression through a variable — an item of an inline list `("{{Url1}}")` or a value of `{% assign url = "{{Url1}}" %}` — is sent as literal text. Build such a reference in the expression itself: `{{ name | prepend: "{{" | append: "}}" }}`.
- `--dump-expanded` and the Trace log show the reference, not the value.

### Advanced: Referencing an Expanded Request by Name

Because request names differ per iteration (`CreatePartner1`, `CreatePartner2`, ...), a later request cannot reference a fixed name like `{{CreatePartner.response...}}`. Combine `forloop.index` with the `prepend`/`append` filters to build the reference dynamically. The `{{` and `}}` must be written in this same expression (see [Values From Data Stay Data](#values-from-data-stay-data)):

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

Request names must be unique after expansion. A duplicate `# @name` is an error, as in a file without templates, with a single exception: a name that only loops repeat.

If you name a request with `# @name CreatePartner`, **every** iteration produces a request with that same literal name. TeaPie logs a warning for this (naming the file, the duplicated name, and how many requests share it) but does not fail the run — only the last-registered request remains resolvable by that name. Always include `{{ forloop.index }}` (or another per-iteration value) in the name to keep it unique:

```http
# @name CreatePartner{{ forloop.index }}
```

A repeated name is only a warning when it is written literally (without `{{ }}`), only inside `{% for %}` loops, and at most once in each loop body. This also covers nested loops and several loops that each declare the name once. Every other duplicate fails the run when the second request with that name is reached:

| Duplicate | Result |
| --- | --- |
| `# @name Login` once in a loop body (also a nested loop, or an `{% if %}` that skips some iterations) | Warning |
| `# @name Login` once in each of several loops | Warning |
| Two requests named `Login` outside loops | Error |
| A request named `Login` outside loops, and a loop that also generates `Login` | Error |
| `# @name Login` twice in the same loop body | Error |
| `# @name Login{{ forloop.index }}` twice in the same loop body (both produce `Login1`, `Login2`, ...) | Error |
| `# @name Login` in both branches of `{% if %}...{% else %}` outside loops | No duplicate — only one branch is rendered |
| `# @name Login` in both branches of `{% if %}...{% else %}` in a loop body | Error as soon as the loop generates a second `Login` (even when every iteration takes the same branch) — the name is written twice in the loop body |

When the duplicated name also comes from a loop, the error suggests including `{{ forloop.index }}` in the name or renaming the other requests.

A name built with `{{ }}` is only known after rendering, so it is not checked against the literal names in the same loop body. If the duplicate-name warning reports more requests than expected (one per iteration of each loop that declares the name), look for another `# @name` in the loop body whose expression renders the same name — for example `# @name {{ n }}` after `{% assign n = "Login" %}` next to `# @name Login`. Such a duplicate is only a warning, although it is a mistake.

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

A `{% for %}` can be nested inside another `{% for %}`. Use `{% assign %}` to capture the outer loop's `forloop.index` under a different name before the inner loop starts, since the inner loop's own `forloop` shadows the outer one:

```http
{% for company in Companies %}
{% assign companyIndex = forloop.index %}
{% for license in company.Licenses %}
### Create license {{ companyIndex }}.{{ forloop.index }}: {{ company.Name }} / {{ license }}
# @name CreateLicense{{ companyIndex }}_{{ forloop.index }}
## TEST-EXPECT-STATUS: [201]
POST {{ApiBaseUrl}}/companies/{{ company.Name }}/licenses
Content-Type: application/json

{ "type": "{{ license }}" }
{% endfor %}
{% endfor %}
```

All requests produced by a top-level loop (the outer loop and everything nested inside it) count together against the 1000-request limit.

## Conditions and Assignments

Inside a loop body — or anywhere at the top level of the file, even with no loop involved — you can also use `{% if %}` / `{% elsif %}` / `{% else %}` / `{% unless %}` and `{% assign %}`. These are plain Fluid tags with their usual semantics; TeaPie adds no special behavior beyond making its own variables (see [How Expansion Works](#how-expansion-works-and-what-it-leaves-alone)) and `forloop` available to the conditions.

```http
{% for partner in Partners %}
{% if partner.IsPreferred %}
### Create preferred partner {{ forloop.index }}: {{ partner.Name }}
POST {{ApiBaseUrl}}/partners/preferred
{%- elsif forloop.last %}
### Create last partner {{ forloop.index }}: {{ partner.Name }}
POST {{ApiBaseUrl}}/partners
{%- else %}
### Create partner {{ forloop.index }}: {{ partner.Name }}
POST {{ApiBaseUrl}}/partners
{%- endif %}
Content-Type: application/json

{ "name": "{{ partner.Name }}" }
{% endfor %}
```

The `{%-` on the `elsif`, `else` and `endif` lines removes those lines from the output. Without it, each of them would leave an empty line between the request line and `Content-Type`, which would end the headers and send `Content-Type: application/json` as part of the body — see [Tag Lines and Whitespace Control](#tag-lines-and-whitespace-control).

A few things worth knowing:

- A condition on an **undefined** name (a typo, or a TeaPie variable that was never set) is falsy rather than an error — this is standard Fluid behavior, not a TeaPie-specific guard. The same holds for a missing key of a dictionary or JSON item. A missing member of a typed item (e.g. `{% if partner.Typo %}`) is an error, see [A Variable](#1-a-variable).
- Variables tagged as `secret` are not visible to templates. In a condition or an `{% assign %}` they behave like an undefined name, even when a lower-priority scope has a non-secret variable with the same name. They also cannot be used as a loop source (see [Guards and Error Handling](#guards-and-error-handling)).
- A dotted TeaPie variable name (e.g. `Temp.Flag`) can only be a loop source. In a condition, an `{% assign %}` or a rendered `{{ }}` expression, Fluid reads it as the member `Flag` of a variable `Temp`, which doesn't exist. So `{% if Temp.Flag %}` is always false, without an error. `{{ }}` can't be written inside a tag, so `{% if {{ Temp.Flag }} %}` is a parse error. To use such a value, copy it to a name without dots in the `-init.csx` script, e.g. `tp.SetVariable("HasFlag", tp.GetVariable<bool>("Temp.Flag"));`, and write `{% if HasFlag %}`.
- `{% assign %}` is local to the file being expanded. It never sets or overrides a TeaPie variable, so scripts, other test cases and the variables cache never see it. To share a value beyond the file, set it with `tp.SetVariable(...)` in the `-init.csx` script.
- Within the file, `{% assign %}` follows standard Liquid semantics: a value is visible from where it is assigned to the end of the file, in conditions and `{{ }}` expressions alike. A value assigned in a loop body is also visible in the following iterations and keeps its last value after the loop. If the assignment never runs (for example inside an `{% if %}` that is false), the name is empty.
- An assigned name that is also a TeaPie variable hides that variable within the file, from the assignment on. TeaPie logs a warning for it; rename the assigned name if you meant to use the variable. Such a name can't be the source of a `{% for %}` loop that follows the assignment: that is an error, because the loop would still read the TeaPie variable. To filter or sort a collection, prepare it in the `-init.csx` script.

## Supported Tags

| Tag | Use |
| --- | --- |
| `{% for %}` ... `{% endfor %}` | Loops (see above). An `{% else %}` branch inside the loop is rendered only by an inner loop whose source comes from an enclosing loop's item (e.g. `company.Customers`), for each outer item whose collection is empty (see [Empty Inner Collections](#empty-inner-collections)). An empty collection of any other loop, a top-level one included, is an error even with an `{% else %}` branch, and in a loop over a numeric range the `{% else %}` branch itself is an error (see [Guards and Error Handling](#guards-and-error-handling)) |
| `{% if %}` / `{% elsif %}` / `{% else %}` / `{% endif %}`, `{% unless %}` / `{% endunless %}` | Conditions (see [Conditions and Assignments](#conditions-and-assignments)) |
| `{% assign %}` | Values local to the file (see [Conditions and Assignments](#conditions-and-assignments)) |
| `{% raw %}` ... `{% endraw %}` | Literal text such as `{%` or `{{ item }}`, written into the expanded file as is. A TeaPie reference such as `{{ApiBaseUrl}}` in it is still resolved afterwards, as anywhere else in the file |
| `{% comment %}` ... `{% endcomment %}` | Text left out of the expanded file; tags inside it are ignored. A `{% comment %}` alone doesn't make a file a template (see [How It Fits the Pipeline](#how-it-fits-the-pipeline)) |

Other Liquid tags are not supported. Fluid still processes some of them in a templated file (e.g. `{% case %}`, `{% capture %}`, `{% cycle %}`, `{% break %}`, `{% continue %}`, `{% increment %}`), but TeaPie does not take them into account, so their results are not guaranteed and may change in a future version. For example, a `{% capture %}` value appears only in a `{{ }}` expression that also references a loop variable, `forloop` or an assigned name, and `{% include %}` / `{% render %}` fail, because templates can't read files. Tags that Fluid doesn't know, such as `{% tablerow %}`, are a parse error. `{% liquid %}` is rejected with an error naming its position, because Fluid would read the rest of the file as its statements.

### Tag Lines and Whitespace Control

A tag writes nothing itself, but the line break after it stays in the output. A tag on its own line therefore leaves an empty line behind. Between requests (before a `###` line) or in a body that is harmless, but between the request line and the headers, or between two headers, an empty line ends the headers, so everything after it is sent as the body.

Liquid's whitespace control removes that whitespace: `{%-` removes the whitespace and the line break **before** the tag, `-%}` the whitespace and line breaks **after** it (the same works for `{{-` and `-}}`). Write `{%-` at the start of a tag that stands on its own line among headers to remove exactly that line:

```http
POST {{ApiBaseUrl}}/partners
Content-Type: application/json
{%- if partner.Trace %}
X-Trace: on
{%- endif %}

{ "name": "{{ partner.Name }}" }
```

Avoid `-%}` at the end of such a line: it also removes the following line breaks, including the empty line between the headers and the body. With `{%- endif -%}` in the example above, the body is glued to the last header and the request fails with an error about an invalid `Content-Type` value. A `###` that whitespace control moves to the end of the previous line still starts a new request, because TeaPie recognizes `###` anywhere in a line.

## Guards and Error Handling

Problems with loop sources and with the structure of a template fail loudly instead of silently producing zero or empty requests. The exception is a name inside a condition or a rendered `{{ }}` expression: an undefined, secret or dotted name, or a missing key of a dictionary or JSON item, is falsy or empty there (see [Conditions and Assignments](#conditions-and-assignments)).

| Situation | Result |
| --- | --- |
| Collection variable does not exist | Error naming the missing variable and the file* |
| Variable exists but is not a collection (a string counts as not a collection, it is never iterated character by character) | Error stating the variable must be a collection* |
| Variable is tagged as `secret` | Error — its values would otherwise appear in the expanded file, in logs and in `--dump-expanded` output* |
| Collection resolves to zero items (empty list, `()`, or a numeric range with no items) | Error, also when the loop has an `{% else %}` branch — an accidentally empty collection is almost always a mistake* (for an **inner** loop, a log message instead — see [below](#empty-inner-collections)) |
| A filter in a loop source (e.g. `{% for x in Items \| sort %}`), an unknown or duplicated loop option | Error naming the source and listing the supported [loop options](#loop-options) |
| A `limit:`/`offset:` value that is not a whole number from 0 to 1000 (e.g. `limit: -1`, `offset: 2.5`) | Error naming the option and the value — see [Loop Options](#loop-options) |
| A loop's collection has more than **1000** items (a top-level source or a fixed inner source such as `(1..5000)` is checked before rendering; an inner source taken from an outer item, such as `company.Licenses`, each time that inner loop starts) | Error naming the loop and its item count. `limit:` does not help: the whole collection counts, because it is loaded before `limit:` applies |
| A top-level loop, together with the loops nested in it, would generate more than **1000** requests (each top-level loop in a file is counted on its own) | Error as soon as the 1001st request is generated, to prevent runaway expansion |
| A numeric range bound (e.g. `(1..99999999999)`) is too large to fit a 32-bit integer | Error naming the offending bound, instead of a raw overflow failure* |
| Missing `{% endfor %}`, a stray `{% endfor %}` with no matching `{% for %}`, or malformed `{% for %}` syntax | Error identifying the malformed tag. A malformed `{% for %}` tag (e.g. a missing `in`, item or source, a missing `%}`, no space around `in`, or a source or options continued on the next line) is reported with its text, its position and the supported form, e.g. `invalid '{% for %}' tag '{% for i (1..2) %}' at (3:1). Write it on one line as '{% for <item> in <collection> %}', for example '{% for car in Cars %}'.` (A file whose only tag is a stray `{% endfor %}` is not a template, so it gets the warning below instead.) |
| A `{% raw %}` without its `{% endraw %}` | Error naming the position of the `{% raw %}`, e.g. `missing '{% endraw %}' for the '{% raw %}' at (4:5).` |
| A `{% liquid %}` tag | Error naming its position — write each statement as a separate tag instead |
| A loop source that is a TeaPie variable reassigned with `{% assign %}` before the loop, e.g. `{% assign Cars = Cars \| where: "Active", true %}` | Error naming the source and the loop's position — prepare the collection in the `-init.csx` script instead |
| An `{% else %}` branch in a loop over a numeric range, e.g. `{% for i in (1..3) %}` ... `{% else %}` ... `{% endfor %}` | Error naming the loop and its position — Fluid does not support `{% else %}` there. An `{% else %}` of a loop over a collection variable or an inline list such as `(1, 2, 3)` works, and so does an `{% else %}` of an `{% if %}` or of a nested loop inside the range loop |
| A file contains `{%` but no supported tag (see [How It Fits the Pipeline](#how-it-fits-the-pipeline)) | Warning (not an error); the file is sent unchanged |
| Nested `{% for %}` loops | Supported — see [Multiple Loops in One File](#multiple-loops-in-one-file); a top-level loop's combined request count (across all its nesting levels) is what counts against the 1000-request limit above |
| An item property referenced in the loop body does not exist (e.g. `{{ partner.Typo }}`) | Error naming the missing member and its first use, when the item is a typed object (also in `{% if %}`). A missing key of a dictionary or JSON item is an empty value, not an error |
| Rendering the file requires more than **200 000** Fluid evaluation steps (e.g. many `{{ }}`/`{% %}` expressions repeated across items, or nested loops that together run too many times) | Error suggesting the file be checked for repeated expressions or split into smaller loops — this is separate from, and on top of, the 1000-request cap |
| Loops nested more than **10** levels deep | Error naming the depth and the innermost loop, before anything is rendered |
| The loops in the file would expand to more than **10 000 000** characters in total | Error as soon as the limit is passed |
| A filter (e.g. `append`, `concat`) or `{% capture %}` produces a text longer than **10 000 000** characters or a list with more than **100 000** items, typically a value built from itself in a loop such as `{% assign s = s \| append: s %}` | Error naming the filter or the captured variable, so the run fails fast |
| Scanning the file for template syntax takes longer than **1 second** in one step. This is a safety net against a run that would hang; it isn't expected to trigger for a real file — even a 30 MB unclosed `{%` ends with a parse error long before it | Error asking to check the file for an unclosed `{{`, `{%` or quote, instead of the run hanging |
| Two or more requests share the same `# @name` after expansion | Error, except for a name written literally only inside loops and at most once per loop body, which is a warning — see [Naming Requests Inside a Loop](#naming-requests-inside-a-loop) |
| An inner loop's collection is empty for every outer item, so it generated no requests | Warning (not an error) — see [Empty Inner Collections](#empty-inner-collections) |
| A loop whose body declares requests generated none (e.g. `offset:` past the end, `limit: 0`, or an `{% if %}` that excluded every item) | Warning (not an error), naming the loop and the file. A loop placed inside a request body (e.g. building a JSON array) declares no requests and is not checked |

All errors include the request file's path to make them actionable. The numeric limits above (1000 items per loop, 1000 requests per top-level loop, 10 000 000 characters, 100 000 list items, 200 000 rendering steps, nesting depth 10) are fixed and cannot be configured. They stop runaway expansion early, but they don't put a hard bound on memory use: text outside top-level loops is not counted, and a filter's result is measured only after it has been computed.

\* These guards apply to a loop's own source expression. A **top-level** source is resolved once, before rendering. An **inner** loop whose source references an ancestor loop's variable (e.g. `company.Licenses` in the [nested-loop example](#multiple-loops-in-one-file) above) is instead checked during rendering, every time that inner loop is about to run, in exactly the same scope the loop itself sees — so TeaPie variables, `{% assign %}` values and enclosing loop variables (including indexers such as `company.Groups[group]`) resolve the same way in the check as in the loop. A missing property or a value that isn't a collection (including `null`) is an error, unconditionally, the same as for a top-level source, and names the offending outer item(s) as a path such as `region[0].country[1]` — the item's position in its source collection, even when the outer loop uses `offset:` or `reversed`. The one exception is an **empty** per-iteration collection — see [Empty Inner Collections](#empty-inner-collections). The 1000-request and malformed-tag guards are unaffected by any of this.

### Empty Inner Collections

An inner loop's source may legitimately be empty for some outer items — a newly created company with no customers yet is a valid case, not a mistake — so it is **not** an error. That outer item simply produces no requests from the inner loop (or renders the loop's `{% else %}` branch, if it has one), while everything else in the file runs normally:

```http
{% for company in Companies %}
### Create company {{ company.Name }}
POST {{ApiBaseUrl}}/companies

{% for customer in company.Customers %}
### Add customer {{ customer.Name }} to {{ company.Name }}
POST {{ApiBaseUrl}}/companies/{{ company.Name }}/customers
{% endfor %}
{% endfor %}
```

So that this never happens silently, TeaPie reports it in the log:

| Situation | Log message |
| --- | --- |
| The inner collection is empty for **some** outer items | **Debug** (visible with `--debug`), one per empty outer item, naming it — e.g. `Loop over 'company.Customers' in 'seed-req.http' produced zero items for company[1], so it generated no requests there.` |
| The inner collection is empty for **every** outer item, so the inner loop generated no requests at all | **Warning** (visible by default), once per loop — e.g. `Loop over 'company.Customers' in 'seed-req.http' produced zero items for every one of its 2 outer item(s), so it generated no requests at all. Check that the collection is filled as expected.` Not logged when the inner loop's own `{% else %}` branch declares a request (e.g. `{% else %}` + `### No customers` + `POST …`), because that branch then generates requests for every outer item; the Debug messages are still logged. |

The emptiness is checked against the source collection, before any `limit:`/`offset:` is applied. An `{% else %}` that belongs to an `{% if %}` or to a nested loop inside the body is not the loop's own branch. If the `{% else %}` branch's requests are excluded too (e.g. by an `{% if %}`), the outer loop's warning that it generated no requests still applies.

## Inspecting the Expanded Content

The expanded requests file only ever exists in memory. To see exactly what is about to run, use the `--dump-expanded` or `--dump-expanded-dir` option of `teapie test`:

```bash
teapie test ./Tests --dump-expanded                     # print each expanded file to the console
teapie test ./Tests --dump-expanded-dir ../expanded     # write each expanded file into ../expanded, keeping its relative path
```

`--dump-expanded-dir` turns the dump on by itself, so `--dump-expanded` is not needed with it. `--dump-expanded` alone can't be combined with `-q|--quiet`, which silences the console, so TeaPie stops with an error; use `--dump-expanded-dir` in quiet runs. The console wraps long lines to its width (80 characters when the output is redirected), so use `--dump-expanded-dir` to get the exact expanded content.

The dump directory must be **outside** the tested collection. A dumped `-req.http` file inside the collection could overwrite the original template, or be picked up as an extra test case by the next run. TeaPie therefore checks the dump directory before the first test case runs and stops with an error instead of writing it there. When testing a single file, only that file's folder counts as the collection. Keep the dump directory outside of any folder you later run as a collection.

If the expansion of a file fails, nothing is dumped for it. The error names the file and the tag that caused it.

A rendered value with a line break, `###`, `{{` or `}}` appears in the dump as a reference such as `{{__teapie_value_1}}` (see [Values From Data Stay Data](#values-from-data-stay-data)), so a dumped file can't be run on its own.

Only files that contain templates are dumped, after their `-init.csx` script has run, so the output matches what is actually executed. The requests are still sent as usual — the dump is a by-product of a normal run (expansion depends on variables set by scripts and earlier requests, so it can't be produced without running them). A `.tp` file is written with an extra `.http` extension (e.g. `seed.tp.http`), since only its request part is expanded. When the `.tp` file holds named test cases (`--- TESTCASE <Name>`), each of them is written separately with its name in the file name (e.g. `seed.tp.First.http` and `seed.tp.Second.http`), and the console header shows it as `seed.tp (First)`. Characters that a file name can't contain are replaced with `_`.

The expansion is also logged:

- **Debug level** — logs the content length before and after expansion (e.g. `120 -> 340`), so you can tell at a glance whether a loop actually expanded anything.
- **Trace level** — logs the **entire** expanded `.http` content for the file, exactly as it is about to be split into individual requests.

Trace is the most detailed logging level and is off by default. Enable it with:

```bash
teapie test -v                                              # verbose console output (includes Trace)
teapie test --log-file debug.log --log-file-log-level Trace # write Trace-level logs, incl. expanded content, to a file
```

See [Logging](logging.md) for all available logging levels and options.

## Reporting

Each request produced by a loop is reported exactly like any other request; there is no separate "loop" concept in the JUnit report. The example below is the report for a test case `Create-Partners-req.http` with:

- a 3-item loop (`{% for partner in Partners %}`), each iteration named `CreatePartner{{ forloop.index }}` and asserting `## TEST-EXPECT-STATUS: [201]`;
- a post-response script that checks each iteration's own response through `tp.Responses["CreatePartner1"]`, `tp.Responses["CreatePartner2"]`, and so on.

```xml
<testsuites name="Create-Partners" tests="5" skipped="0" failures="0" time="0.054" timestamp="...">
  <testsuite name="Create-Partners" tests="5" skipped="0" failures="0" time="0.054">
    <testcase name="[1] Status code should match one of these: [201]" ... />
    <testcase name="[2] Status code should match one of these: [201]" ... />
    <testcase name="[3] Status code should match one of these: [201]" ... />
    <testcase name="All three loop-generated requests should return 201 (Created)." ... />
    <testcase name="Each loop iteration should have sent its own partner's data (not a copy of one)." ... />
  </testsuite>
</testsuites>
```

All requests from the loop (and any plain requests in the same file) land in the **same** `<testsuite>`, since a test suite corresponds to one `.http`/`.tp` file, not one request. A failing iteration reports as a normal `<failure>` on its own `<testcase>`, with no effect on the other iterations' results.

## Current Limitations

This is a first iteration of templating support. The following are **not** supported yet:

- Any Liquid/Fluid tag besides those listed in [Supported Tags](#supported-tags)
- Templating inside `.csx` scripts (only `.http`/`.tp` request content is expanded)
- External data files (CSV/JSON) as a collection source
- Filters in a loop source (see [Loop Options](#loop-options))
- A top-level loop source other than a TeaPie variable name (dotted names included), an inline literal list or a numeric range — a member path (`Root.Children`), an indexer (`Regions[0].Countries`) or a variable created with `{% assign %}` cannot be looped over at the top level. An inner loop's source is evaluated per outer item only when it starts with an enclosing loop's variable (`company.Licenses`, `company[key]`); any other inner source is resolved like a top-level one
- Parallel execution of the requests produced by a loop
