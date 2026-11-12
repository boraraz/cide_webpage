import {
  Boxes,
  FolderTree,
  History,
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

/** Planned sections stay visible and disabled until their module exists. */
export const navigation: readonly NavigationItem[] = [
  { label: 'Dashboard', icon: LayoutDashboard, status: 'available', to: '/' },
  { label: 'Products', icon: Package, status: 'available', to: '/products' },
  { label: 'Categories', icon: FolderTree, status: 'available', to: '/categories' },
  { label: 'Collections', icon: Layers, status: 'available', to: '/collections' },
  { label: 'Stock', icon: Boxes, status: 'available', to: '/inventory' },
  { label: 'Movements', icon: History, status: 'available', to: '/inventory/movements' },
  { label: 'Homepage', icon: Palette, status: 'available', to: '/content/homepage' },
  { label: 'Orders', icon: ReceiptText, status: 'available', to: '/orders' },
  { label: 'Customers', icon: Users, status: 'planned' },
  { label: 'Reviews', icon: MessageSquareText, status: 'planned' },
  { label: 'Media Library', icon: Images, status: 'planned' },
  { label: 'Settings', icon: Settings, status: 'planned' },
];
