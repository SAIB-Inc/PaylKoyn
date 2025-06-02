import { CardanoWallet } from "./types";

declare global {
    interface Window {
        cardano: Record<string, CardanoWallet>;
        listWallets: () => Array<any>;
        getWalletById: (id: string) => any;
    }
}

export { };