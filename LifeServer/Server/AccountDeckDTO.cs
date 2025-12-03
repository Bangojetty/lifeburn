namespace Server;

public class AccountDeckDTO {
    public AccountData accountData;
    public DeckData deckData;

    public AccountDeckDTO(AccountData accountData, DeckData deckData) {
        this.accountData = accountData;
        this.deckData = deckData;
    }
}