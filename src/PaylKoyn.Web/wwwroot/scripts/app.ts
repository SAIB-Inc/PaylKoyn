import {getWallets} from "./bifrost";

window.listWallets = () => {
    return getWallets().map(wallet => ({
        id: wallet.id,
        name: wallet.name,
        icon: wallet.icon,
        apiVersion: wallet.apiVersion
    }));
};

window.getWalletById = (id: string) => {
    const wallets = getWallets();
    return wallets.map(wallet => {
        return {
            id: wallet.id,
            name: wallet.name,
            icon: wallet.icon,
            apiVersion: wallet.apiVersion
        };
    }).find(wallet => wallet.id === id) || null;
}

window.connectWalletById = async (id: string) => {
    const wallets = getWallets();
    const wallet = wallets.find(wallet => wallet.id === id);
    await wallet?.enable();
};