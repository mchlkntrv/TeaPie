# Templating Loops Reference

Expand a single request block in a `.http`/`.tp` file into many independent requests, driven by a collection — for data-driven scenarios (seeding N entities, running the same check against a list of inputs) without copy-pasting request blocks or writing imperative loops in `.csx`.

**Requires:** TeaPie 1.8.0 or newer. Older versions send the tags as literal text.

**Syntax:** `{% for <item> in <source> %}` ... `{% endfor %}` (write the whole `{% for %}` tag on one line, with spaces around `in`)

**Where:** `.http` request files and `.tp` files (in their `--- HTTP` section)

Loop expansion runs **after** the pre-request (`-init.csx`) script and **before** requests are split into individual test steps, so a variable set via `tp.SetVariable(...)` is already available as a loop source. Each resulting request behaves exactly like a normal request: its own name, its own directives, its own report entry. Only a file that contains a supported tag (`{% for %}`, `{% if %}`, `{% unless %}`, `{% assign %}` or `{% raw %}`) is templated. A file using only `{% if %}`/`{% assign %}` with no `{% for %}` is still parsed and rendered (see [Conditions and assignments](#conditions-and-assignments) below). Any other file is left completely unchanged, even if it contains `{%` text (e.g. a `"{%Y-%m-%d}"` body); TeaPie logs a warning for it in case a tag is misspelled. In a templated file, every `{%`, including on `#` comment lines, is Fluid syntax: wrap literal `{%` text in `{% raw %}...{% endraw %}`. A commented-out loop still runs.

## Collection Sources

**1. A variable** (most common — typically set in `-init.csx`):

```http
{% for partner in Partners %}
### Create partner {{ forloop.index }}: {{ partner.Name }}
# @name CreatePartner{{ forloop.index }}
POST {{ApiBaseUrl}}/partners
Content-Type: application/json

{ "name": "{{ partner.Name }}", "registrationId": "{{ partner.RegistrationId }}" }
{% endfor %}
```

Property access is `{{ item.PropertyName }}` and is **case-sensitive**: a misspelled member of a typed item (anonymous type, record, class) is an error, also in `{% if %}`. Items of a JSON body read with `GetBodyAsExpando()` can be looped over directly and their names are not case-sensitive. A missing key of such an item, or of a dictionary or `JArray` item, is empty, not an error. Dotted variable names work as a loop source: `{% for partner in Temp.FreePartners %}`.

**2. An inline literal list:** `{% for status in ("new", "used", "certified") %}` — comma-separated double-quoted strings, booleans (`true`/`false`), or numbers (`1`, `1.5`). An empty list is an error.

**3. A numeric range:** `{% for i in (1..5) %}` — inclusive on both ends (5 iterations). Both bounds must be whole numbers from 0 written directly; a variable (`(1..Count)`), negative or decimal bound is an error.

**Loop options** follow the collection, space-separated, in any order: `limit: n`, `offset: n`, `reversed` (standard Liquid; `n` must be a whole number from 0 to 1000, written directly or held in a variable — anything else, including `offset: continue`, is an error) — e.g. `{% for p in Partners offset: 1 limit: 10 reversed %}`. A loop source does **not** accept filters (`{% for x in Items | sort %}` is an error).

**Top-level sources** must be a TeaPie variable name, an inline list or a range — not a member path (`Root.Children`), an indexer or an `{% assign %}` variable.

## The `forloop` Object

| Field | Meaning |
| --- | --- |
| `forloop.index` | 1-based iteration number |
| `forloop.index0` | 0-based iteration number |
| `forloop.first` | `true` on the first iteration |
| `forloop.last` | `true` on the last iteration |

Usable anywhere in the loop body, including inside `# @name` declarations — expansion happens before request names are parsed.

A `{{ }}` expression is rendered during expansion when it references the loop variable, `forloop` or an `{% assign %}` name anywhere in it (e.g. `{{ "p-" | append: partner.Id }}`); any other name in such an expression is read from TeaPie's variables, and an undefined, secret or dotted name there renders empty, without an error. Every other `{{ }}` (`{{ApiBaseUrl}}`, `{{$guid}}`, request references) is left for TeaPie to resolve after expansion.

The result of each `{{ }}` (after its filters) that contains a line break or `###`, or `{{`/`}}` not written as text in that same expression, is not written into the file: expansion writes a reference such as `{{__teapie_value_1}}`, which TeaPie resolves to the value inside the already split request, like `{{variable}}` in a file without templates. So data can't add requests or headers, and `{{apiKey}}` in data is sent as literal text. Filters and conditions see the real value (`{{ note | json }}` gives valid JSON). To build a TeaPie reference, write the braces in the expression: `{{ forloop.index | prepend: "{{Temp.Id_" | append: "}}" }}`; braces that come through an inline list item or an `{% assign %}` value are sent literally. `--dump-expanded` shows the reference.

## Naming Requests Inside a Loop

Always include `{{ forloop.index }}` (or another per-iteration value) in `# @name`, otherwise every iteration shares one name — the last-registered request wins and TeaPie logs a warning (not a failure). In nested loops, combine the outer and inner index (see the example below). Every other duplicate fails the run: two requests with the same name outside loops, a name outside loops that a loop also generates, the same name written twice in one loop body (also `Name{{ forloop.index }}` written twice, and a literal name in both branches of an `{% if %}...{% else %}` inside a loop body, once the loop generates it twice). A repeated name is only a warning when it is written literally, only inside loops and at most once per loop body. Because names differ per iteration, a later request referencing an iteration's response needs `forloop.index` combined with the `prepend`/`append` filters, e.g. `{{ forloop.index | prepend: "Temp.Attachments.CompanyId_" }}`, to build the variable reference dynamically.

## Nested Loops

A `{% for %}` can be nested inside another `{% for %}`, and a file can contain multiple loops (nested or sibling) plus plain requests between them. The inner loop's own `forloop` shadows the outer one — capture the outer index first with `{% assign %}`:

```http
{% for company in Companies %}
{% assign companyIndex = forloop.index %}
{% for license in company.Licenses %}
### Create license {{ companyIndex }}.{{ forloop.index }}: {{ company.Name }} / {{ license }}
# @name CreateLicense{{ companyIndex }}_{{ forloop.index }}
POST {{ApiBaseUrl}}/companies/{{ company.Name }}/licenses
Content-Type: application/json

{ "type": "{{ license }}" }
{% endfor %}
{% endfor %}
```

All requests produced by a top-level loop (outer loop + everything nested inside it) count together against the 1000-request limit; `limit:` and `{% if %}` are taken into account because the generated requests are counted.

## Conditions and assignments

Inside a loop body — or at the top level with no loop at all — `{% if %}`/`{% elsif %}`/`{% else %}`/`{% unless %}` and `{% assign %}` are also supported, with plain Fluid semantics. An undefined name in a condition is falsy, not an error, and so is a missing key of a dictionary or JSON item. A missing member of a typed item (`{% if partner.Typo %}`) is an error. A dotted TeaPie variable name (e.g. `Temp.Flag`) can only be a loop source. In a condition, `{% assign %}` or rendered `{{ }}`, it is read as member access on `Temp` (undefined), so `{% if Temp.Flag %}` is always false. `{% if {{ Temp.Flag }} %}` is a parse error. Copy the value to a name without dots in `-init.csx` (`tp.SetVariable("HasFlag", tp.GetVariable<bool>("Temp.Flag"));`, then `{% if HasFlag %}`). `{% assign %}` is local to the file: it never sets or overrides a TeaPie variable (use `tp.SetVariable(...)` in `-init.csx` to share a value). Inside the file it follows Liquid semantics: a value is visible from where it is assigned to the end of the file (conditions and `{{ }}` alike), so a loop-body assignment persists into later iterations and after the loop. Don't assign a name that is also a TeaPie variable: it hides the variable within the file (TeaPie logs a warning), and a later loop over that name is an error (the loop would still read the TeaPie variable; filter or sort the collection in `-init.csx`).

**Supported tags:** `{% for %}` (its `{% else %}` branch renders only in an inner loop whose source comes from an outer loop's item, e.g. `company.Customers`, for an outer item with an empty collection; an empty top-level collection is an error even with `{% else %}`, and `{% else %}` in a loop over a numeric range is an error), `{% if %}`/`{% elsif %}`/`{% else %}`, `{% unless %}`, `{% assign %}`, `{% raw %}` (literal text) and `{% comment %}` (left out; alone it doesn't make a file a template). Other Liquid tags are not supported. Fluid still processes some (`case`, `capture`, `cycle`, `break`, `continue`, `increment`), but without any guarantee. A `capture` value only shows in a `{{ }}` that also references a loop variable, `forloop` or an assigned name. `include`/`render` fail, unknown tags (`tablerow`) are a parse error, and `liquid` is rejected with an error naming its position.

**Tag lines and whitespace control:** a tag on its own line leaves an empty line behind. That is harmless before `###` or in a body. Between the request line and the headers, or between two headers, it ends the headers, so the rest is sent as the body. Start such a tag with `{%-` (removes the line break before it) to drop exactly that line:

```http
POST {{ApiBaseUrl}}/partners
Content-Type: application/json
{%- if partner.Trace %}
X-Trace: on
{%- endif %}

{ "name": "{{ partner.Name }}" }
```

Don't end it with `-%}`: that also removes the empty line before the body, which glues the body to the last header and fails the request. A `###` moved to the end of the previous line still starts a new request.

## Guards and Error Handling

Problems with loop sources and template structure fail loudly instead of silently producing zero or empty requests. The exception is names in conditions and rendered `{{ }}`: an undefined, secret or dotted name, or a missing dictionary/JSON key, is falsy or empty there.

| Situation | Result |
| --- | --- |
| Collection variable does not exist / isn't a collection / resolves to zero items | Error — naming the file and the problem* |
| Loop source is a variable tagged `secret` | Error — secret variables are never visible to templates (in conditions/`assign` they act as undefined)* |
| A numeric range bound (e.g. `(1..99999999999)`) is too large for a 32-bit integer | Error naming the offending bound, instead of a raw overflow failure* |
| A loop's collection has more than **1000** items (checked before rendering for fixed sources, when an inner loop starts for sources taken from an outer item); `limit:` does not help | Error naming the loop and its item count |
| A top-level loop (with its nested loops) would generate more than **1000** requests | Error, as soon as the 1001st request is generated |
| Rendering needs more than **200 000** Fluid evaluation steps (too many `{{ }}`/`{% %}` expressions across items, or nested loops that together run too many times) | Error — separate from, and on top of, the 1000-request cap |
| Loops nested more than **10** levels deep | Error naming the depth and the innermost loop, before rendering |
| The loops in the file would expand to more than **10 000 000** characters in total | Error, as soon as the limit is passed |
| A filter or `{% capture %}` produces a text longer than **10 000 000** characters or a list with more than **100 000** items (e.g. `{% assign s = s \| append: s %}` in a loop) | Error naming the filter or the captured variable |
| Missing `{% endfor %}`, a stray `{% endfor %}`, or malformed `{% for %}` syntax | Error identifying the malformed tag; a malformed `{% for %}` tag (missing `in`/item/source/`%}`, no space around `in`, or source/options continued on the next line) is shown with its text, position and the supported form |
| A `{% raw %}` without its `{% endraw %}` | Error naming the position of the `{% raw %}` |
| A `{% liquid %}` tag | Error naming its position; write each statement as a separate tag |
| A loop source that is a TeaPie variable reassigned with `{% assign %}` before the loop (e.g. `{% assign Cars = Cars \| where: "Active", true %}`) | Error naming the source and the loop's position; prepare the collection in `-init.csx` |
| `{% else %}` branch in a loop over a numeric range such as `(1..3)` | Error naming the loop and its position (Fluid does not support it); use a collection variable or an inline list such as `(1, 2, 3)` |
| Scanning the file for template syntax takes longer than **1 s** in one step (a safety net, not expected to trigger for a real file) | Error suggesting to check for an unclosed `{{`, `{%` or quote |
| A filter in a loop source, or an unknown/duplicated loop option | Error listing the supported loop options |
| An item property referenced in the loop body does not exist (e.g. `{{ partner.Typo }}`) | Error naming the missing member, for a typed item (also in `{% if %}`); empty for a missing dictionary/JSON key |
| Two or more requests share the same `# @name` after expansion | Error, except for a name written literally only inside loops and at most once per loop body: warning (last one wins) |
| An inner loop's collection is empty for **every** outer item (it generated no requests at all) | Warning, not an error (not logged when the loop's own `{% else %}` branch declares a request) |
| A loop whose body declares requests generated none (`offset:` past the end, `limit: 0`, an `{% if %}` excluding every item) | Warning, not an error |

All of the above errors include the request file's path. The numeric limits are fixed (not configurable).

\* For an **inner** loop whose source references an ancestor loop's variable (e.g. `company.Licenses`, `company[key]`), TeaPie checks that source during rendering, each time the inner loop is about to run, in the same scope the loop sees (TeaPie variables, `{% assign %}` values, enclosing loop variables). Missing/`null`/non-collection is still an unconditional error there (naming which outer item by its source position, e.g. `region[0].country[1]`), same as a top-level source — but an **empty** per-iteration collection is not an error (a company with no customers is often valid, not a mistake): that outer item just produces no requests from the inner loop (or renders its `{% else %}` branch). It is never silent, though — each empty outer item gets a **Debug** log message naming it (e.g. `company[1]`), and if the collection is empty for **every** outer item, so the inner loop generated no requests at all, a **Warning** (visible by default) names the loop and the file — unless the loop's own `{% else %}` branch (not one of an `{% if %}` or a nested loop) declares a request, since that branch then generates requests for every outer item. If a file must not run with such data, assert it in the `-init.csx`/`-test.csx` script instead.

## Current Limitations

- No Liquid/Fluid tags besides the supported ones listed under [Conditions and assignments](#conditions-and-assignments)
- No templating inside `.csx` scripts (only `.http`/`.tp` request content is expanded)
- No external data files (CSV/JSON) as a collection source
- No parallel execution of the requests produced by a loop

To see what a templated file expands to, run `teapie test --dump-expanded` (prints each expanded file to the console) or `teapie test --dump-expanded-dir <directory>` (writes them there, keeping relative paths; the directory must be outside the tested collection). Requests are still sent — the dump is a by-product of a normal run.

For logging/inspection details (Debug/Trace-level expansion logging) and a full JUnit reporting example, see the [Templating Loops documentation](https://www.teapie.fun/docs/templating-loops.html).
