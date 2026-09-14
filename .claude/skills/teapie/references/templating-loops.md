# Templating Loops Reference

Expand a single request block in a `.http`/`.tp` file into many independent requests, driven by a collection — for data-driven scenarios (seeding N entities, running the same check against a list of inputs) without copy-pasting request blocks or writing imperative loops in `.csx`.

**Syntax:** `{% for <item> in <source> %}` ... `{% endfor %}`

**Where:** `.http` request files and `.tp` files (in their `--- HTTP` section)

Loop expansion runs **after** the pre-request (`-init.csx`) script and **before** requests are split into individual test steps, so a variable set via `tp.SetVariable(...)` is already available as a loop source. Each resulting request behaves exactly like a normal request: its own name, its own directives, its own report entry. Files without a `{% for %}` tag are left completely unchanged.

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

Property access is `{{ item.PropertyName }}` and is **case-sensitive**. Dotted variable names work too: `{% for partner in Temp.FreePartners %}`.

**2. An inline literal list:** `{% for status in ("new", "used", "certified") %}` — comma-separated double-quoted strings, booleans (`true`/`false`), or numbers (`1`, `1.5`). An empty list is an error.

**3. A numeric range:** `{% for i in (1..5) %}` — inclusive on both ends (5 iterations).

## The `forloop` Object

| Field | Meaning |
| --- | --- |
| `forloop.index` | 1-based iteration number |
| `forloop.index0` | 0-based iteration number |
| `forloop.first` | `true` on the first iteration |
| `forloop.last` | `true` on the last iteration |

Usable anywhere in the loop body, including inside `# @name` declarations — expansion happens before request names are parsed.

## Naming Requests Inside a Loop

Always include `{{ forloop.index }}` (or another per-iteration value) in `# @name`, otherwise every iteration shares one name — the last-registered request wins and TeaPie logs a warning (not a failure). Because names differ per iteration, a later request referencing an iteration's response needs `forloop.index` combined with the `prepend`/`append` filters, e.g. `{{ forloop.index | prepend: "Temp.Attachments.CompanyId_" }}`, to build the variable reference dynamically.

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

All requests produced by a nesting-root loop (outer loop + everything nested inside it) count together against the 1000-request expansion limit.

## Guards and Error Handling

Templating fails loudly instead of silently producing zero or empty requests:

| Situation | Result |
| --- | --- |
| Collection variable does not exist / isn't a collection / resolves to zero items | Error — naming the file and the problem* |
| Loop would expand to more than **1000** requests | Error |
| Missing `{% endfor %}`, a stray `{% endfor %}`, or malformed `{% for %}` syntax | Error identifying the malformed tag |
| An item property referenced in the loop body does not exist (e.g. `{{ partner.Typo }}`) | Error naming the missing member |
| Two or more requests share the same `# @name` after expansion | Warning, not an error |

\* For an **inner** loop whose source references an ancestor loop's variable (e.g. `company.Licenses` above), TeaPie cannot pre-resolve it, so these guards aren't enforced — a missing/empty per-iteration source there silently produces zero requests for that outer item instead of erroring.

## Current Limitations

- No Liquid/Fluid tag besides `{% for %}` / `{% endfor %}` / `{% if %}` / `{% elsif %}` / `{% else %}` / `{% unless %}` / `{% assign %}`
- No templating inside `.csx` scripts (only `.http`/`.tp` request content is expanded)
- No external data files (CSV/JSON) as a collection source
- No parallel execution of the requests produced by a loop

For logging/inspection details (Debug/Trace-level expansion logging) and a full JUnit reporting example, see the project's `docs/docs/templating-loops.md` documentation page.
