using FModa.RA;


/// <summary>
/// 鏁版嵁绠＄悊鍣?鈥斺€?鎵€鏈夋暟鎹腑蹇冪殑缁熶竴娉ㄥ唽鍏ュ彛銆?///
/// 璁捐鍘熷垯锛?///   - 鎳掑姞杞斤細鏁版嵁涓績鍦ㄧ涓€娆¤璁块棶鏃惰嚜鍔ㄥ垱寤猴紝涓嶄細涓€寮€濮嬪氨鍏ㄩ儴瀹炰緥鍖?///   - 缁熶竴鍏ュ彛锛氭墍鏈夋暟鎹腑蹇冮€氳繃 DataManager.xxx 璁块棶锛屼笉鍒嗘暎鍒?GF
///   - 鑱岃矗鍗曚竴锛氬彧绠＄悊鏁版嵁涓績鐢熷懡鍛ㄦ湡锛屼笌妯″潡绯荤粺瑙ｈ€?///
/// 浣跨敤鏂瑰紡锛?///   <code>
///   // 鐩存帴璁块棶锛屾寜闇€鑷姩鍒涘缓
///   DataManager.Combat.SetHp(id, 50);
///
///   // 鏂板涓€涓暟鎹腑蹇冨彧闇€鍔犱竴涓睘鎬э細
///   private static InventoryDC _inventory;
///   public static InventoryDC Inventory => _inventory ??= new InventoryDC();
///   </code>
/// </summary>
public static class DataManager
{
    // 鈹€鈹€ 娉涘瀷鏁版嵁琛紙鍐锋暟鎹?閰嶇疆锛夆攢鈹€

    private static GenericTable _generic;
    public static GenericTable Generic => _generic ??= new GenericTable();

    // 鈹€鈹€ 鎴樻枟鏁版嵁锛堢儹鏁版嵁/typed 浜嬩欢锛夆攢鈹€

    private static CombatDataCenter _combat;
    public static CombatDataCenter Combat => _combat ??= new CombatDataCenter();

    // 鈹€鈹€ 棰勫姞杞斤紙鍙€夛級鈹€鈹€

    /// <summary>
    /// 鎻愬墠鍒濆鍖栨寚瀹氱殑鏁版嵁涓績锛堥潪蹇呴』锛屼笉璋冪敤涔熶細鍦ㄩ娆¤闂椂鑷姩鍒涘缓锛夈€?    /// 闇€瑕佸湪娓告垙鍚姩鏃跺氨鍑嗗濂界殑鏁版嵁鍦ㄨ繖閲?new銆?    /// </summary>
    public static void PreWarm()
    {
        // 渚嬶細鎴樻枟绯荤粺鍦ㄦ父鎴忎竴寮€濮嬪氨浼氱敤鍒?        _combat = new CombatDataCenter();

        // 鍚庣画鍔狅細
        // _player = new PlayerDC();
    }

    /// <summary> 娓呯悊鎵€鏈夋暟鎹紙鍦烘櫙鍒囨崲鏃惰皟鐢級</summary>
    public static void ClearAll()
    {
        _generic?.ClearAll();
        _combat?.Clear();
        // 瀛愮被鍚勮嚜瀹炵幇 Clear
    }
}

