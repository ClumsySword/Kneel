using System;
using System.Collections.Generic;
using UnityEngine;

namespace Kneel.Hazards
{
    // A standing strip of crop: a single line of fire cells, ordered along the wind. Fire starts at the upwind
    // end and walks down the row on a timer. It never leaves the row and never turns back, so it plays the
    // same every time.
    public class CropRow : MonoBehaviour, ILevelResettable
    {
        [SerializeField]
        private FireTuning tuning;

        [SerializeField]
        private WindField wind;

        // Upwind first. Sorted at edit time (context menu), never at runtime.
        [SerializeField]
        private List<FireCell> cells = new List<FireCell>();

        private bool burning;
        private float elapsed;
        private int nextCell;
        private int ashCells;

        public IReadOnlyList<FireCell> Cells => cells;

        public bool IsBurning => burning;

        public bool IsBurnedOut { get; private set; }

        public event Action<CropRow> OnRowBurnedOut;

        private void Awake()
        {
            foreach (var cell in cells)
            {
                cell.OnBurnOut += HandleCellBurnOut;
            }
        }

        // Lights the upwind cell; each next cell starts to smoulder one cell delay after the one before.
        public void Ignite()
        {
            if (burning == true || cells.Count == 0 || cells[0].State != FireCellState.Dry)
            {
                return;
            }

            burning = true;
            elapsed = 0f;
            nextCell = 0;
            ashCells = 0;
        }

        public void ResetToDry()
        {
            burning = false;
            IsBurnedOut = false;
            elapsed = 0f;
            nextCell = 0;
            ashCells = 0;
            foreach (var cell in cells)
            {
                cell.ResetState();
            }
        }

        public void ResetState()
        {
            ResetToDry();
        }

        // Edit time: the whole row starts as ash (already burned).
        public void SetStartInAsh(bool ash)
        {
            foreach (var cell in cells)
            {
                cell.SetStartInAsh(ash);
            }
        }

        [ContextMenu("Sort Cells Along Wind")]
        public void SortCellsAlongWind()
        {
            Vector3 direction = wind != null ? wind.Direction : Vector3.forward;
            cells.Sort((a, b) => Vector3.Dot(a.transform.position, direction).CompareTo(Vector3.Dot(b.transform.position, direction)));
        }

        private void FixedUpdate()
        {
            if (burning == false)
            {
                return;
            }

            elapsed += Time.fixedDeltaTime;
            while (nextCell < cells.Count && elapsed >= nextCell * tuning.cellDelay)
            {
                cells[nextCell].Smoulder();
                nextCell++;
            }
        }

        private void HandleCellBurnOut(FireCell cell)
        {
            ashCells++;
            if (burning == true && ashCells >= cells.Count)
            {
                burning = false;
                IsBurnedOut = true;
                OnRowBurnedOut?.Invoke(this);
            }
        }

        private void OnDrawGizmos()
        {
            if (cells.Count < 2 || cells[0] == null || cells[cells.Count - 1] == null)
            {
                return;
            }

            // The way the fire runs.
            Vector3 from = cells[0].transform.position + Vector3.up * 2.2f;
            Vector3 to = cells[cells.Count - 1].transform.position + Vector3.up * 2.2f;
            Vector3 along = (to - from).normalized;
            Vector3 side = Vector3.Cross(Vector3.up, along);
            Gizmos.color = new Color(1f, 0.45f, 0.1f);
            Gizmos.DrawLine(from, to);
            Gizmos.DrawLine(to, to - along * 1.2f + side * 0.6f);
            Gizmos.DrawLine(to, to - along * 1.2f - side * 0.6f);
        }
    }
}
