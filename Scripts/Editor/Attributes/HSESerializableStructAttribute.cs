using System;

/// <summary>Миры размещения локального компонента на экземпляре. Both не включает репликацию.</summary>
public enum HSEComponentWorld : byte
{
    Server = 1,
    Client = 2,
    Both = Server | Client
}

/// <summary>Регистрация wire-типа. Сетевые типы размещаются в обоих мирах до ghost-конвертации.</summary>
[AttributeUsage(AttributeTargets.Struct)]
public class HSESerializableStructAttribute: Attribute
{
    public uint ID {  get; }
    public bool StateOnly { get; set; }
    public string FormerTypeName { get; set; }
    /// <summary>Миры локального компонента. Для сетевого типа генератор всегда выбирает Both.</summary>
    public HSEComponentWorld World { get; set; } = HSEComponentWorld.Server;

    /// <summary>Назначает постоянный ID через JSON-историю при регистрации типа.</summary>
    public HSESerializableStructAttribute() { }

    /// <summary>Сохраняет поддержку явно заданного ненулевого ID существующего типа.</summary>
    public HSESerializableStructAttribute(uint id){
        if (id == 0) throw new ArgumentException($"[HSESerializableStruct] ID must be higher then 0");
        this.ID = id; 
    }
}
