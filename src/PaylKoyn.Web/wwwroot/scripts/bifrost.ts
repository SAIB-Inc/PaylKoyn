import { CardanoWallet } from "./types";

export function getWallets(): Array<CardanoWallet> {
    
    if(typeof window === 'undefined') return [];

    const result: Array<CardanoWallet> = [];
    const cardanoNamespace = window.cardano;

    if (!cardanoNamespace) return result;

    Object.entries(cardanoNamespace).forEach(([key, wallet]: [string, CardanoWallet]) => {
        if (wallet?.name && wallet?.apiVersion) {
            result.push({...wallet, id: key});
        }
    });

    return result;
}