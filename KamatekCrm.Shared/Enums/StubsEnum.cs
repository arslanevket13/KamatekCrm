using KamatekCrm.Shared.Enums;

namespace KamatekCrm.Shared.Enums
{
    public enum ServiceJobType { Fault, Project }
    public enum WorkflowStatus { Draft, Approved }
    public enum PurchaseStatus { Pending, Ordered, Shipped, Received, Cancelled, Completed }
    public enum WarehouseType { MainWarehouse, Mobile, Other, Vehicle }
    public enum StockTransactionType { In, Out, Sale, Purchase, AdjustmentPlus, AdjustmentMinus, OpeningStock, Transfer, Adjustment, ReturnToSupplier, ReturnFromCustomer, ServiceUsage }
    public enum AuditActionType { Create, Update, Delete, Login, Logout, View, PasswordChange, PasswordReset }
    public enum ProductCategory { Camera, Intercom, FireAlarm, Security, SmartHome, Network, Other, Cable, BurglarAlarm, AccessControl, Satellite, FiberOptic }
    public enum ProductCategoryType { Camera, Intercom, FireAlarm, Security, SmartHome, Network, Other, Cable, BurglarAlarm, AccessControl, Satellite, FiberOptic }
    
    public enum AttachmentEntityType { ServiceJob, Customer, Product, Quote, Invoice }
    public enum PaymentMethod { Cash, CreditCard, BankTransfer, MobilePayment, Check }
    public enum PipelineStage { New, Qualification, Proposal, Negotiation, Negotiating, Won, Lost, Lead, Quoted }

    public enum TransactionType { Income, Expense, Transfer, Debt, Payment }
    public enum CashTransactionType { Income, Expense, Transfer, CashIncome, CardIncome, TransferIncome, CashExpense, CardExpense, TransferExpense }
    
    // JobPriority removed (defined in JobPriority.cs)

    public enum JobCategory { Security, Network, Automation, Electrical, Maintenance, Fault, Other, CCTV, VideoIntercom, FireAlarm, BurglarAlarm, SmartHome, AccessControl, SatelliteSystem, FiberOptic }
    
    public enum JobType { Standard, Project, Emergency, Maintenance, SecurityCamera, VideoIntercom, SatelliteSystem }
    
    public enum StatusFilter { All, Active, Completed, Cancelled, Pending, Tümü, Bekleyen, DevamEden, Tamamlanan }
    
    public enum StructureType { Building, Floor, Room, Outdoor, Rack, Other, SingleUnit, Apartment, Site, Commercial }
    public enum DeviceType { Camera, Sensor, Panel, Keypad, Switch, Router, NVR, AccessPoint, Other, IpCamera }
    public enum NodeType { Root, Group, Item, Project, Block, Entrance, Floor, Flat, Garden, Parking, Zone, CommonArea }
    public enum AssetStatus { Active, Inactive, Maintenance, Repair, Scrapped, Lost, Stolen, NeedsRepair }


    // RepairStatus removed (defined in RepairStatus.cs)
}
