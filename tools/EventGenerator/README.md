# Event Source Generator

This source generator automatically creates event subscription boilerplate for the Python Events API.

## Usage

Simply add the `[GenApiEvent("EventName")]` attribute to a partial method declaration in the `Events` class:

```csharp
/// <summary>
/// Subscribe to player hits changed event. Callback receives the new hits value as an integer.
/// </summary>
/// <param name="callback">Python function to call when player hits change</param>
[GenApiEvent("OnPlayerHitsChanged")]
public partial void OnPlayerHitsChanged(object callback);
```

## What Gets Generated

For each attributed method, the generator creates:

1. **Private event handler field**
   ```csharp
   private EventHandler<int> _onPlayerHitsChangedHandler;
   ```

2. **Complete method implementation**
   ```csharp
   public partial void OnPlayerHitsChanged(object callback)
   {
       UnsubscribeOnPlayerHitsChanged();

       if (callback == null || !_engine.Operations.IsCallable(callback))
           return;

       _onPlayerHitsChangedHandler = (sender, arg) =>
       {
           _api?.ScheduleCallback(callback, arg);
       };

       EventSink.OnPlayerHitsChanged += _onPlayerHitsChangedHandler;
   }
   ```

3. **Private unsubscribe method**
   ```csharp
   private void UnsubscribeOnPlayerHitsChanged()
   {
       if (_onPlayerHitsChangedHandler != null)
       {
           EventSink.OnPlayerHitsChanged -= _onPlayerHitsChangedHandler;
           _onPlayerHitsChangedHandler = null;
       }
   }
   ```

## Features

- **Automatic type detection**: The generator reads the event signature from `EventSink` to determine the correct `EventHandler<T>` type
- **Standard handler pattern**: All handlers use `(sender, arg) =>` for consistency
- **Automatic unsubscription**: Calling the subscribe method again automatically unsubscribes the previous handler
- **Thread-safe callbacks**: All callbacks are routed through `ScheduleCallback()` for proper thread synchronization

## Adding New Events

1. Ensure the event exists in `ClassicUO.Game.Managers.EventSink`
2. Add the partial method declaration with the attribute to `Events.cs`
3. Build the project - the implementation is generated automatically

Example:
```csharp
[GenApiEvent("OnSetWeather")]
public partial void OnSetWeather(object callback);
```

## Python Usage

Once generated, Python scripts can use the events like this:

```python
def on_hits_changed(new_hits):
    print(f"Player hits: {new_hits}")

API.Events.OnPlayerHitsChanged(on_hits_changed)

# The callback will be automatically called whenever player hits change
# To unsubscribe, just call with None or a new callback:
API.Events.OnPlayerHitsChanged(None)  # Unsubscribes
```