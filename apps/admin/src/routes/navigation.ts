import {
  Boxes,
  FolderTree,
  Images,
  Layers,
  LayoutDashboard,
  type LucideIcon,
  MessageSquareText,
  Package,
  Palette,
  ReceiptText,
  Settings,
  Users,
} from 'lucide-react';

export type NavigationItem =
  | { label: string; icon: LucideIcon; status: 'available'; to: string }
  | { label: string; icon: LucideIcon; status: 'planned' };

/** Planned sections are rendered disabled until their module and admin auth exist. */
export const navigation: readonly NavigationItem[] = [
  { label: 'Dashboard', icon: LayoutDashboard, status: 'available', to: '/' },
  { label: 'Products', icon: Package, status: 'planned' },
  { label: 'Categories', icon: FolderTree, status: 'planned' },
  { label: 'Collections', icon: Layers, status: 'planned' },
  { label: 'Inventory', icon: Boxes, status: 'planned' },
  { label: 'Orders', icon: ReceiptText, status: 'planned' },
  { label: 'Customers', icon: Users, status: 'planned' },
  { label: 'Reviews', icon: MessageSquareText, status: 'planned' },
  { label: 'Appearance', icon: Palette, status: 'planned' },
  { label: 'Media Library', icon: Images, status: 'planned' },
  { label: 'Settings', icon: Settings, status: 'planned' },
];
