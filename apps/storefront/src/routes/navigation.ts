export interface StorefrontNavItem {
  label: string;
  to: string;
}

/** Category and collection links are loaded from the API once those modules exist. */
export const primaryNavigation: readonly StorefrontNavItem[] = [{ label: 'Home', to: '/' }];
