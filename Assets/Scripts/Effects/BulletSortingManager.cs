public static class BulletSortingManager
{
    // 各サイズの現在のカウント（初期値は範囲の最小値）
    private static int smallCounter = 11000;
    private static int mediumCounter = 8000;
    private static int largeCounter = 5000;

    public static int GetNextOrder(BulletSize size)
    {
        int order = 0;
        switch (size)
        {
            case BulletSize.Small:
                order = smallCounter;
                smallCounter++;
                if (smallCounter > 13999) smallCounter = 11000;
                break;
            case BulletSize.Medium:
                order = mediumCounter;
                mediumCounter++;
                if (mediumCounter > 10999) mediumCounter = 8000;
                break;
            case BulletSize.Large:
                order = largeCounter;
                largeCounter++;
                if (largeCounter > 7999) largeCounter = 5000;
                break;
        }
        return order;
    }
}