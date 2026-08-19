using System;
using System.Collections.Generic;
using Combat.Component.Ship;

namespace Combat.Scene
{
    public class ShipList : IUnitList<IShip>
    {
        public ShipList()
        {
            _lockObject = new object();
            _ships = new List<IShip>();
        }

        // Readers (notably the background AI workers) get an immutable array
        // snapshot. Structural mutations stay on _ships under _lockObject, so
        // multiple target-selection workers can scan concurrently without
        // serializing on the collection lock.
        public IReadOnlyList<IShip> Items => GetSnapshot();
        public object LockObject => _lockObject;

        public void Add(IShip ship)
        {
            lock (_lockObject)
            {
                _ships.Add(ship);
                _snapshotDirty = true;
            }
        }

        public void Remove(IShip ship)
        {
            lock (_lockObject)
            {
                var index = _ships.IndexOf(ship);
                _ships.QuickRemove(index);
                _snapshotDirty = true;
            }
        }

        public void Clear()
        {
            lock (LockObject)
            {
                _ships.Clear();
                _snapshot = Array.Empty<IShip>();
                _snapshotDirty = false;
            }
        }

        private IReadOnlyList<IShip> GetSnapshot()
        {
            if (!_snapshotDirty)
                return _snapshot;

            lock (_lockObject)
            {
                if (_snapshotDirty)
                {
                    _snapshot = _ships.ToArray();
                    _snapshotDirty = false;
                }

                return _snapshot;
            }
        }

        private readonly object _lockObject;
        private readonly List<IShip> _ships;
        private IShip[] _snapshot = Array.Empty<IShip>();
        private volatile bool _snapshotDirty = true;
    }
}
