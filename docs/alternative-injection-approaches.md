# Alternative Toolbar Injection Approaches

If Approach A (VisualElement Tree Injection) breaks in a future Unity version, here are two fallback strategies.

## Approach B — IMGUI Callback Hook via Reflection

Hook into the GameView's `OnGUI` or toolbar drawing method via reflection, appending a button draw call after Unity's own toolbar code runs.

### How it works

1. Use reflection to get the `GameView.DoToolbarGUI()` method (or its equivalent)
2. Create a Harmony-style patch or delegate wrapper that calls the original method, then draws the fullscreen toggle button at the end of the toolbar
3. The button is drawn in the same IMGUI pass as the rest of the toolbar, so it gets perfect pixel alignment

### Implementation sketch

```csharp
// Get the toolbar draw method
var doToolbarGUI = s_GameViewType.GetMethod(
    "DoToolbarGUI",
    BindingFlags.Instance | BindingFlags.NonPublic);

// Option 1: Override via MethodInfo.Invoke wrapper
// Option 2: Use Harmony (if available) to postfix the method
// Option 3: Hook into EditorApplication.update and draw after
//           the toolbar IMGUI pass completes
```

### Pros
- Draws directly in the IMGUI pass — perfect pixel alignment with existing buttons
- No VisualElement tree dependency
- Button naturally reflows with the toolbar

### Cons
- Deep reflection into internal IMGUI methods — highest fragility
- Harmony dependency would be new (or manual IL patching)
- Method signature could change between Unity versions
- Harder to implement clean no-op fallback (patching can have side effects)
- The method name `DoToolbarGUI` is not guaranteed stable

### When to use
- If Unity moves the toolbar from VisualElement to pure IMGUI (unlikely but possible)
- If the VisualElement tree structure changes so drastically that finding any anchor becomes impossible

---

## Approach C — Hybrid: VisualElement Child Insertion

Instead of absolute positioning, insert the `IMGUIContainer` as a **child** of the toolbar's VisualElement container, letting the layout engine handle positioning.

### How it works

1. Walk the `rootVisualElement` tree to find the toolbar container (same as Approach A)
2. Find the specific child element that represents the Play Mode dropdown
3. Insert the button `IMGUIContainer` as a sibling **after** that element
4. Let FlexBox/VisualElement layout handle positioning — no absolute coordinates

### Implementation sketch

```csharp
var toolbar = FindToolbarContainer(root);
var dropdown = FindPlayModeDropdown(toolbar);

var button = new IMGUIContainer(() => DrawToggle(gameView));
button.style.width = 22;
button.style.height = 18;
button.style.alignSelf = Align.Center;

// Insert after the dropdown
int index = toolbar.IndexOf(dropdown);
toolbar.Insert(index + 1, button);
```

### Pros
- No absolute positioning math — layout engine handles it
- Button reflows naturally if toolbar width changes
- Most "native" feeling injection

### Cons
- Inserting children into Unity's internal VisualElement containers can conflict with Unity's own layout logic (it may overwrite children on repaint)
- Finding the specific dropdown element requires knowing its type/name/class, which may change
- Unity could use `contentContainer` restrictions that prevent adding children
- If Unity rebuilds the toolbar VisualElement tree, inserted children are silently dropped (mitigated by DetachFromPanelEvent re-scan)

### When to use
- If absolute positioning proves too unreliable across multiple monitor DPI configurations
- If Unity stabilizes its VisualElement toolbar structure and documents it
- If the toolbar container allows child insertion without conflicts

---

## Decision Matrix

| Factor | A (Current) | B (IMGUI Hook) | C (Child Insert) |
|--------|:-----------:|:---------------:|:-----------------:|
| Positioning accuracy | Good | Perfect | Perfect |
| Fragility | Low | High | Medium |
| Implementation complexity | Low | High | Medium |
| No-op fallback cleanliness | Clean | Risky | Clean |
| Unity version resilience | Good | Poor | Medium |
| Code simplicity | ~200 lines | ~250 lines | ~180 lines |

**Recommendation:** Start with A. If A breaks, try C. Resort to B only as a last resort.
