// The Partner item below has no 'TaxId' field - demonstrates the
// "undefined loop-item field" guard.
tp.SetVariable("TypoPartners", new[] { new { Name = "Acme Corp" } });
