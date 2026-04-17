namespace ClothingRecycler.Desktop.Models
{
    public sealed class WeightUnitOptionModel
    {
        public WeightUnitOptionModel(WeightUnit unitType)
        {
            UnitType = unitType;
        }

        public WeightUnit UnitType { get; }

        public string DisplayName => WeightUnitHelper.GetDisplayName(UnitType);
    }
}
