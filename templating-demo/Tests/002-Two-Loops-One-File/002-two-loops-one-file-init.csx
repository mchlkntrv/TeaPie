// Two independent collections used by two separate, sequential (not nested)
// loops in the same request file.
tp.SetVariable("Products", new[]
{
    new { Name = "Widget" },
    new { Name = "Gadget" },
    new { Name = "Gizmo" }
});

tp.SetVariable("Categories", new[]
{
    new { Name = "Electronics" },
    new { Name = "Tools" }
});
