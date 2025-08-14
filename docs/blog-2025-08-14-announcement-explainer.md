# Fancy uploading your passport to a porn site? Dread when someone wants to see a bank statement? A new JavaScript library solves this.

Short on time? Read the tl;dr below. If you're interested in proofs, or you work in healthcare, government, finance, conveyancing, insurance, legal services, or sell stuff to adults, and much more besides, then I encourage you to read on. I hand wrote this (it's getting rare these days) to explain how this key technology works for non-technical folks. Feel free to copy paste it into an LLM and ask questions.

The bottom line is that we need to make it easy for web and app developers to create and accept selective-disclosure proofs and exchange verifiable data so that we don't keep oversharing our most personal information. I've built a JS library which does the heavy lifting to try to kick start this future.

Trigger Warning - I talk a fair bit about blockchains, but this technology doesn't necessitate blockchains.

## Too long; didn't read

- **The Problem**: We're forced to share entire documents when we only need to prove basic facts like age or address, violating privacy. We can solve this, but we need to agree on a standard format and make it super easy to use

- **The Solution**: ProofPack enables selective disclosure—reveal only specific information while keeping everything else private

- **For Developers**: Provides a standardized, easy-to-understand format for creating and verifying cryptographic proofs using blockchain attestations and Merkle trees

- **The Goal**: Make selective disclosure so simple that it becomes the norm—only with accessible, standardized tools will developers widely adopt privacy-preserving verification

- **The Impact**: When this becomes standard practice, we all regain control of our personal data

## Full article - a non-techy guide to proofs

Our laws and regulations often force apps and websites to invade our privacy and accumulate sinks of our personal data, violating the principle that the best way to secure data is not to ask for it in the first place. The regulators are aware and are actively researching and encouraging technologies to solve this.

Businesses often need to confirm one or two facts about us, such as our address, age, income, or our nationality, which are printed on certain documents like bank statements. Naturally, businesses can only trust the source document if it's hard to fake and is issued by an authority of some sort, though in reality someone just checks that it looks alright.

What if there was an easy way to share certain information from a document while hiding the rest of it? 

And what if the checker could be highly confident that the data was genuinely taken from a particular record that came from a trustworthy source?

This is exactly what I've built and here's how it works.

## Zim zimma - what happens when we all have keys

In a moment I'll explain how it's possible to reveal one line item from a list of data, while keeping the rest secret and yet incontrovertibly part of the original set of data. By the end of this explainer you'll be able to dazzle your friends at dinner parties (remember your whiteboard). In the meantime, let me explain what an attestation is and how the blockchain plays an important part in your future privacy.

A blockchain is an open database with no central server holding 'the truth'. Most blockchains can execute short program code, small 'apps' which run on the computers making up the chain, are often not 'owned' by anyone and cannot be turned off! What could possibly go wrong? A blockchain, then, is a kind of distributed, open computer. I'll talk about the Ethereum blockchain because it's the one I know most well.

Anyone with technical know-how can read from the Ethereum blockchain, for free, by installing the blockchain software on their PC. Otherwise, you can read it (usually with a free usage plan) via several companies who operate APIs. As you might imagine, blockchains look very much like public utilities, providing almost indestructible and permanent public records.

The short little programs I mentioned above usually store data. To use a program and store data you need to pay a small fee, usually pennies, and you need a kind of login to identify you as a 'user'.

An Ethereum login is essentially a very long and special number created using clever cryptographic math. You end up with two 'keys' (long codes), one is public and can be shared with whoever you like, while the other is private and must be kept secret and safe, for all eternity. Unlike a website password, you cannot reset or change it, so it is a very important secret and many software folks are scratching their heads trying to work out how they can make it easy for billions of internet users to keep such keys safe (or use it as a way to lock you in to their ecosystem).

Your long public key can be shortened to create an 'address', which becomes your 'wallet address'.

You can make as many Ethereum blockchain logins as you like. This is important as it allows you to wear many masks and have many personalities. Should you lose or have your secret key revealed, only that persona is compromised. Many blockchain users have logins holding their money, others they use to login to websites and social media with and more. Each blockchain identity is a random number, not your name or a nickname.

The main thing to understand about these keys is the idea of 'signing' data. Bear with me as I try to explain this as simply as I can. Once you have a pair of private and public keys, you can send your public key to friends and family. Then you can grab some data and then run it through a special algorithm to create another, shorter, lump of data called a 'signature'. You can send the original data + the signature to someone else and, if they have your public key on file, they can use a complementary algorithm to check the signature came from your pair of keys.

As a member of the original Outlook email team for Microsoft in the 90s I can wow you that it has been possible to do this with email for 30 years! So why didn't we? Well, the industry decided it was too hard to teach everyone about cryptography and, crucially, the industry appointed centralised authorities to issue the keys (CAs). The designers of Windows and Mac didn't try to make it simpler either.

Perhaps the most profound shift in moving to blockchains will be in millions of people having a pair of cryptographic keys because lots of important stuff can be built around this eventuality.

Your keys are used to interact with a program on the blockchain. An instruction message is sent to the blockchain along with a signature, and it's this signature which proves that only you, the holder of the private key, could have created the instruction to send granny in Australia 7 Ethereumseses (unless someone stole your keys).

## Attestations in a nutshell, and what happens when everyone can attest to anything

So the programs on these blockchains are fairly small 'applets' which update their records and store little pieces of data. Anyone can write these programs and put them on the blockchain. It is open to all, allowing for new kinds of digital public amenities to be built. No, not like public toilets. Each program has an address, and each user has an address. These programs are known as Smart Contracts or just 'contracts', which is a dumb name - they're just short programs.

The most famous contracts today are 'stablecoins' and 'memecoins'. These are short and simple records of how much of a token a wallet address owns, along with some additional program code to let you send an amount to another address (by simply deducting an amount from one record, and adding it to the other). Because it is so simple, and the code is open to all, anyone can launch a memecoin - literally you can have your very own currency. 

What a time to be alive.

The program I want to talk about is far more useful, called the Ethereum Attestation Service, and it's a very simple program that lets a user on the blockchain issue a short permanent record regarding another user's address; "I am publishing this bit of data about this other address" - your review of your own memecoin contract, perhaps.

EAS has the concept of a sender and a recipient (though the recipient doesn't get notified like an email) and the data attached. For example, a public library might store a record about a member having borrowed a book where the book's ISBN number is the attached data. The attestation is permanent, but it can be updated to signal that it has been revoked (e.g. the book was returned). It's very simple.

The record being totally public doesn't sound very privacy conscious, does it? People can read the blockchain (for free) and see the books being lent out, but remember that both the sender (library) and the recipient (book borrower) is just a random address formed from their random public key, so the actual person is obscured.

If all these activities are public, could AI begin to guess who the real person is behind a wallet address by observing patterns and times? Yes, this is quite realistic and has been widely done with traditional 'anonymous' web traffic for decades.

But we can introduce some privacy using a thing called a 'hash'.

## All the King's horses

When you set your password on a website or an app, if it's built responsibly, then it does NOT store your actual password. But if the website doesn't know your password, then how can it check it when you log in?

The website's password-setting code runs the text through a clever mathematical algorithm which creates a long random code, and it is this random code that's actually stored in the database. All the King's horses and all the King's men cannot put your password back together again from the random code. The code is called a one-way hash and only the original text can recreate it, so it's easy to check if a person has entered the correct password: type it in wrong and the hash won't match what's in the database.

The original data (your password) is called a 'preimage' and, despite all this hashing nonsense, it's still very possible to just guess your preimagine 'password123', so you still need to come up with something unguessable. By the way, the real hash of the text 'hello' using an algorithm called SHA-256 is below, just so you know:

```
5891b5b522d5df086d0ff0b110fbd9d21bb4fc7163af34d08286a2e846f6be03
```

When stored, this hash code has 256 1s and 0s.

Going back to our book library example above, what if the ISBN number was put through a hasher and the random hash code was stored in the public attestation? Now we have obscured the sender, recipient and the book! But wait. We could still get hold of a list of all the ISBNs in the world and run each one through the hasher and stop when it matches the attestation! Now we're back to square one.

What the library can do to protect against this 'dictionary attack', as it's called, is to come up with yet another random code for each book which we can use in conjunction with the ISBN. This other important value is called a 'salt' in cryptography - I assume because it's like the salt crystals we add to help make a mush in a pestle and mortar - and it is kept secret like a password or private key.

The ISBN + the salt are mixed together to make the final hash, so you can't repeatedly guess the ISBN number to try and hit upon the right hash, because you'd have to also guess the myriad combinations of salt. The salt, I should add, looks very much like that random hash example above.

Still with me? You're making great progress. I'm as surprised as you are.

## The proof of the pudding

Now we're in an interesting position. We have public records of which books all the users of the library have borrowed and the records are both openly readable and completely unreadable. A list of public random numbers - it's a beautiful thing.

Now, if a library user wanted to prove that they have spent years studying Winnie the Pooh, then they might ask the library to provide the ISBNs and the salts for each of the relevant public attestations.

A recruiting team, keen to ensure they're hiring an excellent and studious Pooh expert, will need to know a few things.

Were these attestations really made by the library?

Is the person we're interviewing really the recipient of the attestations?

Might they have faked the data?

The last one is simple. Cryptographically, so long as the blockchain can be relied upon, and so long as the salts are long and unguessable, and so long as the hashing algorithm is known to be good and strong, then it is close to impossible to fake.

Now, the first question is a little more tricky since the recruiters need to gain some confidence that the address used to make the attestations, the sender or 'from address', is really the wallet address of the library. To do this, they might use a different mechanism to look up the public key and wallet address of the library, relying on some other source that would be hard to fake. For example, the library's public key that's published on the library's website.

The recruiters might then also want supporting evidence and look in other places where it would be hard to fake, such as old social media posts, or perhaps even something physical on the library building, like its address engraved into ivory. Metal. Metal's better all round. They could check internet DNS records, too.

They'd also want to feel confident that the library hasn't leaked its private key, allowing other actors to make attestations on its behalf, so they could check the news for recent leak announcements, and then look for further onchain attestations using the very same EAS system that have been awarded to the library by security experts to say that the library stores its secrets properly and has good controls in place. They can check for a long history of attestations awarded and received by the library over time.

The second one is also simple. To be sure that their candidate for Pooh expert has the wallet address to which all the attestations we made, they can simply ask them to digitally sign some data using their private key. The recruiters can then use an algorithm to read the private key from the signature, shorten it to form their blockchain wallet address, and make sure it matches the recipient of the attestations. This is essentially how 'Sign in with Ethereum' works on websites; your browser wallet extension pops up and you sign a login message and send it up for validation.

Okay, so we now have a way to record facts publicly, but privately, which can later be revealed and checked - it's really clever, and really rather easy to understand...

## How to reveal just part of some larger record

Now you understand the above, it is pretty simple to extend our library example and demonstrate how it's totally possible and actually pretty easy to prove just a small part of some larger record. We're going to need a tree.

Imagine the library issues a single attestation for a bunch of books lent on a particular visit. There's a set of four books and so there are four ISBN numbers. Now, for this example, the library is not going to attest four times but just once for the whole set. To do this, we're going to create a new random salt for each ISBN and combine them to make a hash for each book (as before), giving us four ISBNs, four salts, and four hashes.

By now you should understand that a hash algorithm takes in some data (of any length) and spits out an ugly, random code called a hash (which is of a fixed length).

If we take our four groups of data (ISBN, salt, hash) we can combine the hashes of the first two books together and make another new hash, i.e. `hashOfBook1 + hashOfBook2 = hashOverBooks1And2`. And then we can combine the hashes for the second two books in the same way so we end up with two new hashes in total. Then, these two hashes can be further combined to make a final hash called the 'root hash' - you can imagine this as a tree, see below.

The root hash would be what the library attests to on the blockchain, covering all four books in the tree. It sort of looks like this:

```
Book A → LeftHash → Root
Book B ↗
Book C → RightHash ↗
Book D ↗
```

Consider that if any of the book data were to change in any way, then the random hashes would be different and the differences would cascade down to the final hash. So we have strong tamper resistance. This tree concept is called a Merkle tree and our book library will store this tree in its private database for later.

### Here's how the trick is done

We can now take this tree and erase the ISBN numbers and salts for all of the books (leaving just their hashes) except for Book B where we retain its ISBN, salt and hash. To be clear, books A, C and D only have their hash values, while B has all three values, revealing B was lent to our Pooh expert.

If we gave this tree to someone and explained how it works, they could recompute the cascading hashes to arrive at the root hash and check it matches what's in the tree they were given, and they'd be sure it has not been tampered with. They would then be sure that Book B was definitely part of the set of original books lent.

They can then check the root hash matches what has been attested to via EAS on the blockchain. And because the attestation can only have been written by someone holding the library's private key, they know that Book B was lent to the recipient by the library in question.

The only thing left to do is ensure the recipient of the attestation really is the person they're dealing with. That's simple, we know that's just a case of asking them to 'sign' some text with their private key and then run it through an algorithm to compare the signature with the wallet address in the attestation.

Bingo. We have our selective disclosure proof! I feel like David Blaine.

All we need now is a helpful precoded widget thing which can do all this and reduce it all down to a few lines of JavaScript. And that is ProofPack, the thing I have built.

## ProofPack and how blockchain attestations are optional

The vision is that when we need to prove something about ourselves, such as our income or funds at hand to buy a house, even our name and phone number (more on that, later), we could open our banking app, download a proof containing only the items we want to reveal or hide, then upload it to the site that wants it. In fact, the banking app could just provide a URL and you slap it into your conveyancing portal.

There are a few obstacles to making this a reality:

1. **The first** is that cryptography is a niche within software engineering, so dealing with cryptographic proofs doesn't come naturally to the coders making our digital products who are focused on their area of product and design specialism.

2. **The second** is that we need to converge on a single way to represent this data; we need a data format everyone understands, like how we can all speak PDF.

3. **The third** is that we need awareness within product development teams that selective-disclosure is possible, easy, and should be the default right thing to do. Apps and websites need to accept a proof and the apps that are fact sources need to produce proofs. Proofs are far more robust than someone merely eyeballing `bank_statement_1.pdf`, '[shrug], hmm looks legit'.

ProofPack is a JavaScript library available as a package on NPM and has a .NET version available on NuGet.org, making it easy to consume. It was developed in-house by me to complement Zipwire's ID checking system to allow users to create selective-disclosure proofs of data from their passport. Of course, this is of little use if no one can accept them.

ProofPack on GitHub

The proof itself is the Merkle tree structure explained above, formatted using JSON, a very popular web data format which uses plain text.

The data items, such as an ISBN number, have to be encoded in text in some manner. That's easy for an ISBN number but less so easy for a mug shot. So the design of the JSON structure carries a content-type property which uses the conventions of the web to describe what the data is, and how a program can decode it.

Simply adding a content-type goes a long way to creating a format for exchanging data; without this, the system reading the proof would have no idea how to decode and use the data, which might even be a full PDF document.

The Merkle tree JSON structure is placed within a simple outer JSON structure which contains an optional block describing who the proof was issued to, a timestamp and a random code unfortunately called a 'nonce' (number used only once), as well as an optional attestations sub-structure which describes where any attestations can be found on the blockchain.

The timestamp is the time when the proof was created. That's useful should the app reading the proof want to reject proofs that seem ancient and could be stolen. The nonce can be stored by the app should it want to reject proofs it has seen before. These additional fields provide a means to block bad actors from using proofs that they've hacked or 'harvested' somehow.

Some applications will want to ensure that the proof was issued to an expected person, most likely the current logged-in user who is being asked to prove something. Other applications, however, may not care who is submitting a proof, for example, someone working at a customs checkpoint uploading proofs obtained from a QR code stuck to a box (the QR code, being publicly scannable, may be less revealing than the proofs a truck driver holds on his device).

If the Merkle root is attested via something like EAS (see above), then this structure will provide strong confidence that the data can be trusted as coming from the attester. But if there's no attestation, for a reader to be sure it is a genuine proof and not simply made-up, then we need another mechanism to be sure that it came from a trusted source.

ProofPack can wrap proofs in a special, digitally signed JSON envelope called a JWS. This is a well known open standard for securely exchanging JSON and is most commonly used as security tokens or JWTs, which are small data payloads used by APIs to authenticate and authorize callers.

The data inside a JWS or JWT cannot be altered without invalidating the digital signature. The outermost JWS envelope means that the blockchain attestation is optional, because the Merkle tree inside is 'protected' by the JWS. The reader is looking to ensure the JWS was signed by a key they recognise and trust.

Note that ProofPack proofs with attestations would still benefit from an outer JWS envelope to protect the issue-to, timestamp and nonce values, otherwise, even though the Merkle tree can be checked and verified against the onchain attestation, the user could enter their own data for these parts.

## Attestations allow self-sovereign proofs

Attestations are recommended, however, because they allow a user to manually construct their own proofs using nothing more than a text editor! A user can download a full proof, i.e. one that contains all data with nothing hidden, and then produce their own selective-disclosure proof simply by deleting the data and salt properties from the 'leaf' nodes of the tree.

The reason this works is because, as discussed above, the cascading Merkle tree hashes will still compute as before and can be verified against the onchain attestation. The timestamp and nonce will be useless, however, since there's no outer JWS to 'sign them off', but the attestation will have a reliable blockchain timestamp.

A side effect of using text-based JSON formats and content-type metadata is that today's LLMs can natively read the encoded proof. Incredibly, today's LLMs can even code and run a small program to verify the Merkle tree by recomputing its hashes! If an LLM had access to 'tools' which can query the Ethereum Attestation Service, it could even verify the tree root hash against the attestation.

The advanced capabilities of LLMs mean that, some day soon, anyone could accept a proof and paste it into their LLM tool and check it, opening up proofs to professionals who do not have websites, apps or software but rely on email exchange of attachments, such as lawyers and conveyancing.

## Proving your name and phone number

A CEO of a blockchain company recently tweeted about his experience trying to figure out whether an old friend texting him from a new number (or at least one he didn't have in his phone), and whether to trust the person.

Blockchain CEOs have to run a default level of high paranoia at all times, but the rest of us should also be suspicious of any unsolicited contact, too. The point of the CEO's tweet was to highlight how our devices don't offer anything to help us verify each other. You'd be tempted to suggest just calling the person and speaking to them, and you'd probably not be fooled even by an AI voice, if you really knew the person well, but what if you don't?

How might ProofPack solve this?

A service which has vetted a person's identity and has a verified phone number on record, could issue a proof revealing these facts. The blockchain CEO could ask their apparent old friend to send over a proof, and they could paste it into an LLM, or use a command line tool or desktop utility, or even forward it to a trusted proof reader WhatsApp bot.

Note that the sender could send the CEO two proofs, one revealing their name and the other revealing their phone number, so long as you're happy with them. Let's talk about how exactly you might become happy with them. Pretend you're the CEO. So, you're on the golf course...

Beep beep beep. The texter from +15550000 says, "Hey it's Matt Long, we worked together a few years ago." You know the name, but you don't know their number.

How can we be sure that this number belongs to someone named Matt Long? We're seeking a good level of confidence that the unrecognised phone number is associated with that name.

Consider some app which has (a) established strong confidence that their customer really is a person named Matt Long and (b) has checked Matt Long has control of a phone. This app is in a position to issue a proof (or two proofs), and Matt could forward them on to you.

With one proof, we'd want to check the timestamp is recent, verify the Merkle proof hashes, then we'd want to check the name and phone number data leaves revealed in the Merkle tree, and then ensure either the JWS is signed with the key of the trusted app. If there's an attestation, then we can look it up onchain (or via EAS website) and check the Merkle root hash matches, is not revoked and is recent, and comes from the key of the trusted app.

With two proofs, we'd want to do everything as before, but also be sure both proofs have the same trusted app's key (again via JWS signature, or attestation).

Software could run all these checks. This is doable.

Okay so now let's play Devil's Advocate and consider that simply being in possession of a proof is not proof that they are actually your old friend - it might be someone who's stolen Matt's phone and tried 1234 to unlock it. One way to gain confidence would be if the proof's outer JWS was signed by Matt's wallet, and in the proof is an attestation that we can check to see its creator is JP Morgan. We kill two birds with one proof.

Usually we'd expect the JWS to be signed by the attester but, in this scenario, by signing it with the attestation recipient's private key we establish that the proof-maker has control of that key. This is effectively a second factor; if a thief has Matt's phone, then they probably do not have access to Matt's banking app because it's locked by a fingerprint. If the proof has a very recent timestamp then it shows whoever is sending the message also currently has Matt's finger.

## Selective Disclosure vs. Zero Knowledge

Before I bring this to a close - and good riddance, because I am not used to having to actually type words any more - let me touch on another amazing and super buzzy privacy preserving technology, Zero Knowledge Proofs.

In short, a ZK proof doesn't even reveal the data, but can answer a true or false question about it. For example, does this person have more than $1,000 in their account? Or, was this person born in the EU? Are they over 18? Stone.

This is different and complementary to selective-disclosure proofs. They're currently bleeding edge technology and consume a fair bit of computing power to execute. They are extremely difficult to explain in simple terms and use some incredible math.

It's almost incomprehensible that it's possible to ask a question and be absolutely certain that the answer is correct, while also learning nothing of the data. However, once you get how the trick is done (and it's a few days of deep tech and math), you realise that like with our proofs above, it is absolutely possible.

There are some new web standards coming which Google's wallet and pay team are working on which will allow ZK proofs to be issued from information on your device, e.g. from an ID document in your wallet. So you could potentially soon click a button on a website, your phone bleeps and you tap to reveal the answer to the question, an age check or whatever, and the website lets you in.

You'd use a ZK proof when you have something like a threshold to meet, such as 18+ or some bounds check, and you'd use a selective-disclosure proof when you actually want to reveal the data, such as filling in a job application form on a website using verifiable data from a proof.

Mercifully, we're at the end.

## Get ProofPack

You can find ProofPack for JS on NPM:

@zipwire/proofpack

@zipwire/proofpack-ethereum

And for .NET on NuGet.org:

Zipwire.ProofPack

Zipwire.ProofPack.Ethereum

Being OSS, anyone can submit a PR with tweaks and fixes or preferably their implementation for Java, Golang, Python and so on.

**Spread the word. ProofPack. Let's stop this oversharing madness ✌️**
