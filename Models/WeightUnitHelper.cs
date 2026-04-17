namespace ClothingRecycler.Desktop.Models
{
    public static class WeightUnitHelper
    {
        public static string GetLabel(WeightUnit unitType) => unitType switch
        {
            WeightUnit.Kilogram => "kg",
            WeightUnit.Jin => "斤",
            WeightUnit.Piece => "件",
            _ => "kg",
        };

        public static string GetDisplayName(WeightUnit unitType) => unitType switch
        {
            WeightUnit.Kilogram => "公斤",
            WeightUnit.Jin => "斤",
            WeightUnit.Piece => "件",
            _ => "公斤",
        };

        public static IReadOnlyList<WeightUnitOptionModel> GetInputOptions(WeightUnit categoryUnitType) => categoryUnitType switch
        {
            WeightUnit.Piece => [new(WeightUnit.Piece)],
            WeightUnit.Kilogram or WeightUnit.Jin => [new(WeightUnit.Kilogram), new(WeightUnit.Jin)],
            _ => [new(WeightUnit.Kilogram)],
        };

        public static bool SupportsInputUnit(WeightUnit categoryUnitType, WeightUnit inputUnitType)
        {
            if (categoryUnitType == WeightUnit.Piece || inputUnitType == WeightUnit.Piece)
            {
                return categoryUnitType == inputUnitType;
            }

            return categoryUnitType is WeightUnit.Kilogram or WeightUnit.Jin
                && inputUnitType is WeightUnit.Kilogram or WeightUnit.Jin;
        }

        public static double NormalizeQuantity(double quantity, WeightUnit unitType)
        {
            var safeQuantity = Math.Max(0, quantity);
            return unitType == WeightUnit.Piece
                ? Math.Round(safeQuantity)
                : Math.Round(safeQuantity, 2);
        }

        public static double ConvertQuantity(double quantity, WeightUnit fromUnitType, WeightUnit toUnitType)
        {
            var normalizedQuantity = NormalizeQuantity(quantity, fromUnitType);
            if (fromUnitType == toUnitType)
            {
                return NormalizeQuantity(normalizedQuantity, toUnitType);
            }

            if (!SupportsInputUnit(toUnitType, fromUnitType))
            {
                throw new InvalidOperationException($"不支持从 {GetDisplayName(fromUnitType)} 切换到 {GetDisplayName(toUnitType)}。");
            }

            return (fromUnitType, toUnitType) switch
            {
                (WeightUnit.Kilogram, WeightUnit.Jin) => NormalizeQuantity(normalizedQuantity * 2, toUnitType),
                (WeightUnit.Jin, WeightUnit.Kilogram) => NormalizeQuantity(normalizedQuantity / 2, toUnitType),
                _ => NormalizeQuantity(normalizedQuantity, toUnitType),
            };
        }

        public static double ConvertUnitPrice(double unitPrice, WeightUnit fromUnitType, WeightUnit toUnitType)
        {
            var normalizedPrice = Math.Round(Math.Max(0, unitPrice), 2);
            if (fromUnitType == toUnitType)
            {
                return normalizedPrice;
            }

            if (!SupportsInputUnit(toUnitType, fromUnitType))
            {
                throw new InvalidOperationException($"不支持从 {GetDisplayName(fromUnitType)} 切换到 {GetDisplayName(toUnitType)} 的单价。");
            }

            return (fromUnitType, toUnitType) switch
            {
                (WeightUnit.Kilogram, WeightUnit.Jin) => Math.Round(normalizedPrice / 2, 2),
                (WeightUnit.Jin, WeightUnit.Kilogram) => Math.Round(normalizedPrice * 2, 2),
                _ => normalizedPrice,
            };
        }
    }
}
