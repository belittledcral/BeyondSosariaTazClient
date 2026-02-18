using System;
using ClassicUO.Game.Data;
using ClassicUO.Game.Managers;

namespace ClassicUO.LegionScripting.PyClasses;

public class PyJournalEntry(JournalEntry entry)
{
    public ushort Hue = entry.Hue;
    public string Name = entry.Name;
    public string Text = entry.Text;

    public TextType TextType = entry.TextType;
    public DateTime Time = entry.Time;
    public MessageType MessageType = entry.MessageType;

    public bool Disposed;
}
