from pathlib import Path
p=Path('Assets/Editor/CardKingSafetyRegression.cs')
s=p.read_text(encoding='utf-8-sig')
s=s.replace('''                foreach (Piece endpoint in rooks) endpoint.cardDefinition = card;''','''                foreach (Piece endpoint in rooks) { endpoint.ClearRpgState(); endpoint.cardDefinition = card; }''')
a=s.index('                // Use a fixture-owned')
b=s.index('                try\n',a)
s=s[:a]+'''                // Supply only missing templates temporarily, restoring the scene library in finally.
                var library = UnityEngine.Object.FindFirstObjectByType<SceneObjectTemplates>(FindObjectsInactive.Include);
                if (library == null) library = root.AddComponent<SceneObjectTemplates>();
                var fixtureEntries = new List<SceneObjectTemplates.Entry>();
                foreach (string key in new[] { "Moat token", "Electric net token" })
                {
                    if (library.templates.Exists(entry => entry.key == key && entry.template != null)) continue;
                    var template = new GameObject(key);
                    template.transform.SetParent(root.transform);
                    template.SetActive(false);
                    var entry = new SceneObjectTemplates.Entry { key = key, template = template, runtimeParent = root.transform };
                    library.templates.Insert(0, entry);
                    fixtureEntries.Add(entry);
                }
'''+s[b:]
s=s.replace('''                    typeof(LogicManager).GetMethod("ClearBoardFieldVisuals", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(logic, null);''','''                    typeof(LogicManager).GetMethod("ClearBoardFieldVisuals", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(logic, null);
                    foreach (var entry in fixtureEntries) library.templates.Remove(entry);''')
p.write_text(s,encoding='utf-8')
