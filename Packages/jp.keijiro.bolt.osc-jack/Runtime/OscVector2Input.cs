using Ludiq;
using OscJack;
using System.Collections.Generic;
using UnityEngine;

namespace Bolt.Addons.OscJack {

[UnitCategory("OSC"), UnitTitle("OSC Input (Vector 2)")]
public sealed class OscVector2Input
  : Unit, IGraphElementWithData, IGraphEventListener
{
    #region Data class

    public sealed class Data : IGraphElementData
    {
        public System.Action<EmptyEventArgs> UpdateAction { get; set; }
        public Vector2 LastValue { get; private set; }
        public bool IsOpened => _port != 0;
        public bool HasNewValue => _queue.Count > 0;

        int _port;
        string _address;
        Queue<Vector2> _queue;

        public void Dequeue()
          => LastValue = _queue.Dequeue();

        public void Open(int port, string address)
        {
            _port = port;
            _address = address;
            _queue = new Queue<Vector2>();

            var server = OscMaster.GetSharedServer(_port);
            server.MessageDispatcher.AddCallback(_address, OnDataReceive);
        }

        public void Close()
        {
            var server = OscMaster.GetSharedServer(_port);
            server.MessageDispatcher.RemoveCallback(_address, OnDataReceive);

            _port = 0;
            _address = null;
        }

        void OnDataReceive(string address, OscDataHandle data)
        {
            lock (_queue)
                _queue.Enqueue(new Vector2(data.GetElementAsFloat(0),
                                           data.GetElementAsFloat(1)));
        }
    }

    public IGraphElementData CreateData() => new Data();

    #endregion

    #region Unit I/O

    [DoNotSerialize]
    public ValueInput Port { get; private set; }

    [DoNotSerialize]
    public ValueInput Address { get; private set; }

    [DoNotSerialize, PortLabelHidden]
    public ControlOutput Received { get; private set; }

    [DoNotSerialize, PortLabelHidden]
    public ValueOutput Value { get; private set; }

    #endregion

    #region Unit implementation

    protected override void Definition()
    {
        isControlRoot = true;
        Port = ValueInput<uint>(nameof(Port), 8000);
        Address = ValueInput<string>(nameof(Address), "/unity");
		Received = ControlOutput(nameof(Received));
        Value = ValueOutput<Vector2>(nameof(Value), GetValue);
    }

    Vector2 GetValue(Flow flow)
      => flow.stack.GetElementData<Data>(this).LastValue;

    #endregion

    #region Graph event listener

    public void StartListening(GraphStack stack)
    {
        var data = stack.GetElementData<Data>(this);
        if (data.UpdateAction != null) return;

        var reference = stack.ToReference();
        data.UpdateAction = args => OnUpdate(reference);

        var hook = new EventHook(EventHooks.Update, stack.machine);
        EventBus.Register(hook, data.UpdateAction);
    }

    public void StopListening(GraphStack stack)
    {
        var data = stack.GetElementData<Data>(this);
        if (data.UpdateAction == null) return;

        var hook = new EventHook(EventHooks.Update, stack.machine);
        EventBus.Unregister(hook, data.UpdateAction);

        if (data.IsOpened) data.Close();
        data.UpdateAction = null;
    }

    public bool IsListening(GraphPointer pointer)
      => pointer.GetElementData<Data>(this).UpdateAction != null;

    #endregion

    #region Update hook

    void OnUpdate(GraphReference reference)
    {
        using (var flow = Flow.New(reference))
        {
            var data = flow.stack.GetElementData<Data>(this);
            if (data.IsOpened)
            {
                while (data.HasNewValue)
                {
                    data.Dequeue();
                    flow.Invoke(Received);
                }
            }
            else
            {
                var port = (int)flow.GetValue<uint>(Port);
                var address = flow.GetValue<string>(Address);
                data.Open(port, address);
            }
        }
    }

    #endregion
}

} // namespace Bolt.Addons.OscJack
