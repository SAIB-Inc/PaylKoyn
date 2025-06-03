export type Paginate = {
    page: number,
    limit: number,
};

type Brand<Base, Tag extends string> = Base & { __brand: Tag };

export type Value = Brand<unknown, 'Value'>;
export type Transaction = Brand<unknown, 'Transaction'>;
export type TransactionUnspentOutput = Brand<unknown, 'TransactionUnspentOutput'>;
export type TransactionWitnessSet = Brand<unknown, 'TransactionWitnessSet'>;
export type CoseSign1 = Brand<unknown, 'CoseSign1'>;
export type CoseKey = Brand<unknown, 'CoseKey'>;

declare const CborHexBrand: unique symbol;
/**
 * A generic phantom type for CBOR-encoded hex strings associated with type `T`.
 * 
 * The actual runtime type is just a string, but the type system will treat it as distinct.
 */
export type CborHex<T> = string & { readonly [CborHexBrand]: T };

export type AddressHex = string;
export type AddressBech32 = string;
export type DataSignature = {
    signature: CborHex<CoseSign1>,
    key: CborHex<CoseKey>,
};

export type CardanoWallet = {
    id: string;
    name: string;
    apiVersion: string;
    icon: string;
    enable(): Promise<CardanoWalletApi>;
    isEnabled(): Promise<boolean>;
};

/**
 * Represents the API for interacting with a Cardano wallet.
 */
export type CardanoWalletApi = {
    /**
     * Gets the network ID.
     * @returns {Promise<number>} A promise that resolves to the network ID.
     */
    getNetworkId: () => Promise<number>;

    /**
     * Gets the used addresses.
     * @returns {Promise<Array<AddressHex>>} A promise that resolves to an array of used addresses.
     */
    getUsedAddresses: () => Promise<Array<AddressHex>>;

    /**
     * Gets the unused addresses.
     * @returns {Promise<Array<AddressHex>>} A promise that resolves to an array of unused addresses.
     */
    getUnusedAddresses: () => Promise<Array<AddressHex>>;

    /**
     * Gets the change address.
     * @returns {Promise<AddressHex>} A promise that resolves to the change address.
     */
    getChangeAddress: () => Promise<AddressHex>;

    /**
     * Gets the reward addresses.
     * @returns {Promise<Array<AddressHex>>} A promise that resolves to an array of reward addresses.
     */
    getRewardAddresses: () => Promise<Array<AddressHex>>;

    /**
     * Gets the balance.
     * @returns {Promise<CborHex<Value>>} A promise that resolves to the balance.
     */
    getBalance: () => Promise<CborHex<Value>>;

    /**
     * Gets the UTXOs.
     * @param {CborHex<Value> | undefined} amount - The amount to filter UTXOs.
     * @param {Paginate | undefined} paginate - Pagination options.
     * @returns {Promise<Array<CborHex<TransactionUnspentOutput>>>} A promise that resolves to an array of UTXOs.
     */
    getUtxos: (amount: CborHex<Value> | undefined, paginate: Paginate | undefined) => Promise<Array<CborHex<TransactionUnspentOutput>>>;

    /**
     * Signs a transaction.
     * @param {CborHex<Transaction>} tx - The transaction to sign.
     * @param {boolean} [partialSign] - Whether to partially sign the transaction.
     * @returns {Promise<CborHex<TransactionWitnessSet>>} A promise that resolves to the signed transaction witness set.
     */
    signTx: (tx: CborHex<Transaction>, partialSign?: boolean) => Promise<CborHex<TransactionWitnessSet>>;

    /**
     * Signs data.
     * @param {AddressHex | AddressBech32} address - The address to sign the data with.
     * @param {string} payload - The data to sign.
     * @returns {Promise<DataSignature>} A promise that resolves to the data signature.
     */
    signData: (address: AddressHex | AddressBech32, payload: string) => Promise<DataSignature>;

    /**
     * Submits a transaction.
     * @param {string} txHex - The transaction hex to submit.
     * @returns {Promise<string>} A promise that resolves to the transaction ID.
     */
    submitTx: (txHex: string) => Promise<string>;

    /**
     * Registers an event listener for the specified event.
     * @param {string} eventName - The name of the event to listen for.
     * @param {Function} callback - The callback function to execute when the event is triggered.
     * @remarks This function is experimental and may not be part of the standard API.
     */
    on(eventName: string, callback: (...args: unknown[]) => unknown): void;

    /**
     * Removes an event listener for the specified event.
     * @param {string} eventName - The name of the event.
     * @param {Function} callback - The callback function to remove.
     * @remarks This function is experimental and may not be part of the standard API.
     */
    off(eventName: string, callback: (...args: unknown[]) => unknown): void;

    /**
     * Experimental features that may not be part of the standard API.
     */
    experimental: {
        /**
         * Gets the collateral UTXOs.
         * @returns {Promise<Array<CborHex<TransactionUnspentOutput>>>} A promise that resolves to an array of collateral UTXOs.
         * @remarks This function is experimental and may not be part of the standard API.
         */
        getCollateral: () => Promise<Array<CborHex<TransactionUnspentOutput>>>;
    };
};