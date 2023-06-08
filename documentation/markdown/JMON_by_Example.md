# JMON by Example

The following document is a tutorial for the JMON data format. It
relies on the reader to have some basic knowledge of JSON, the ways human-edited
JSON is commonly used in software, and programming concepts such as "classes"
and "dictionaries."

It is framed as a story involving a protagonist named Becky Everydame and her
boss Mr. Fail. The story has some entertainment value, but don't get too
invested in it. It contrives enormously in service of its greater mission, to
teach the reader about JMON.

## The Setup

Becky Everydame, our protagonist, is one of the brightest interns ever to work
at Hasdude Entertainment. Her boss is Mr. Fail, a man who comes close to
exceeding his fair share of mistakes over the course of this story, though he
does **not** make a mistake at the onset of this story when he chooses to assign
a task to Becky. The task is to develop an online gallery of characters in the
company's upcoming game, _Transylformers: Dark of the New Moon_. This is to be a
webpage that allows viewers to see rotating 3D models of the characters, and to
read their names.

He meets with Becky to plan the project. For whatever reason, the first thing
Mr. Fail wants to discuss is whether the gallery should automatically maximize 
the browser window used to view it. For this decision, he constructs a
"decision matrix." The matrix he produces looks like this:

|                    | Hardcore Fans                           | Other People             | Search Engine<br/>Crawlers |
|--------------------|-----------------------------------------|--------------------------|----------------------------|
| **Maximize**       | May appreciate<br/>immersive experience | Likely to<br/>be annoyed | Bad for SEO                |
| **Don't Maximize** | Will still enjoy gallery                | Won't be<br/>annoyed     | Good for SEO               |

Labels on the left side of the matrix describe the two alternatives to consider
("Maximize" and "Don't Maximize"). Labels on the top describe 
three different parties who will look at the page ("Hardcore Fans," "Other 
People," and "Search Engine Crawlers"), and by extension, three criteria by 
which to evaluate the alternatives. This decision matrix allows Mr. Fail to see 
that maximizing the browser window would be a bad idea, and so once again, he 
does not make a mistake.

The decision matrix has another effect: it
reminds Becky of a data format called _JMON_, short for 
"JSON-building Matrix Object Notation." She suggests that data used to 
populate the data could be stored in a _JMON Sheet_, and this way, it would 
be easier for her to update the gallery pages and even add new ones.

Mr. Fail agrees, and this isn't a mistake either, but don't get comfortable; 
Mr. Fail is here to goof up and goof up he will. Mr. Fail 
proposes a structure for the data, and in doing so, overlooks some 
important pillars of the _Transylformers_ brand. This is the structure:

- At the root level, a single dictionary whose
    - **keys** are development codenames for each character, and whose
    - **values** are instances of `GalleryPage`, a class which has
        - a `name` field for the character's name (a string), and
        - a `model` field for the name of a 3D model file (also a string).

Having read the _Transylformers_ brand guidelines, Becky has some concerns about
this structure. She expresses them both in this meeting during her 
daily journaling. Nonetheless, she works 
diligently on implementing the gallery and producing the JMON Sheet which 
represents the gallery pages.

The very next morning, Mr. Fail comes to Becky's desk and timidly admits 
that there's a problem. It's one of the problems Becky anticipated!
But before we learn what that problem is, let's take a look at the JMON Sheet
that Becky produced for the online gallery during this first iteration of the
development process. Despite Mr. Fail's goof, it remains valid and purposeful!

## JMON Example #1 (The Basics)

The following figure shows the JMON Sheet that Becky produced for the online
gallery during this first iteration of the development process:

|         | C-0     | C-1             | C-2                |
|---------|---------|-----------------|--------------------|
| **R-0** | `:{`    | `.name`         | `.model`           |
| **R-1** | `.alph` | `Romulus Alpha` | `alph_hunter_mech` |
| **R-2** | `.nosf` | `Nosferaudio`   | `nosf_sound_mech`  |
| **R-3** | `.bear` | `Honey Bear`    | `bear_melee_mech`  |

Becky has wisely chosen to use an _Object Matrix_ as the outermost element of
the JMON Sheet. This structure is denoted by the _Header Cell_ at its top-left
corner containing only the "curlyfrown diglyph" (`:{`). An Object Matrix is 
so named because it represents a _JSON Object_.

Much like how a decision matrix has 
choice and condition labels in its left and top margins, an Object Matrix
has _Paths_ in its margins, which are called the _Path Column_ and _Path 
Row_. Paths are identified by the "magic dot" (`.`) which prefixes them. In 
this case, the Path Column (Rows 1 through 3, Columns 0 through 0) contains
keys for the online gallery's dictionary (development codenames for 
each character). The Path Row (Rows 0 through 0, Columns 1 through 2) contains 
names for the fields of the class `GalleryPage`.

Becky has filled the Object Matrix's _Interior_ (Rows 1 through 3, Columns 1 
through 2) with _Value Cells_, so named because they represent a _JSON Value_.
In this case, since none of the Cells begin with a special character, they 
all represent _Strings_ (a type of JSON Value). Becky intends for each
String to go to a particular field (determined the Cell's column) of a 
particular dictionary entry (determined by the Cell's row). For 
example, the dictionary entry for "alph" will have its `name` field set to 
"Romulus Alpha" (see Row 1, Column 1). The Path Row and Column determine 
this relationship between a Cell's position and the destination for its Value.

### JMON Example #1 as JSON Text

Like any valid JMON Sheet, **JMON Example #1** represents a single JSON Value.
In this case, it's an Object with three _Properties_: 
"alph", "bear", and "nosf". Values for these Property are all JSON Objects with 
two Properties of their own: "name" and "model".

A valid _JSON Text_ also represents a single JSON Value at its root. For this 
reason, it always possible for a _JMON Parser_ to convert a valid JMON Sheet 
into a JSON Text which represents the (exact) same Value.

This is how Becky's webpage-serving code works. It calls upon a JMON Parser 
to convert her JMON Sheet into a JSON Text, and this is the result:
```json
{
    "alph": { "name": "Romulus Alpha", "model": "alph_hunter_mech" },
    "nosf": { "name": "Nosferaudio", "model": "nosf_sound_mech" },
    "bear": { "name": "Honey Bear", "model": "bear_melee_mech" }
}
```

Typically, a JMON Parser does not perform _deserialization_, meaning it does 
not convert JSON Values into data used by a piece of software at runtime.
Becky writes the code for this herself. It deserializes the JSON Text
into a dictionary with strings as keys and instances of the class `GalleryPage`
as values.

> **NOTE:** Now, dear reader, I don't mean to be discouraging, but if you found 
> that last sentence ("It deserializes the JSON Text") to be almost 
> completely incoherent, you're going to need to save this tutorial for 
> another day.
> Return to it after you have gotten yourself up to speed on JSON, the ways
> human-edited JSON is commonly used in software, and programming concepts 
> such as "classes" and "dictionaries."
> If you did understand that sentence but had trouble with some of the 
> others... then that's either my fault or just a fact of life, so 
> regardless I encourage you to keep reading.

### How Is the JSON Object Produced? Feel free to skip this section but please

come back to it later, for although the process by which a JMON Parser turns an
Object Matrix into a JSON Object is intuitive, it is also sufficiently important,
and sufficiently novel for a sufficient number of users, that it deserves an
in-depth explanation.

In the first step of converting an Object Matrix into a JSON Object, the JMON
Parser creates an empty mutable JSON Object, to be referred to later as
"the root Object."

In the second step, it creates a list of something akin to "key-value pairs" by
traversing each Cell of its Interior, adding a list entry for any which is a
Value Cell (or something else; see **JMON Example #5**). But instead of
key-value pairs, this list contains "Path-Path-Value triplets." The Value
(capitalized here because it is always a _JSON Value_) comes from the
Cell's contents. The Paths, through a simple cross-referencing process, come
from the Object Matrix's _Path Column_ and _Path Row_. When a Parser parses 
**JMON Example #1**, this list contains:

- A triplet with Paths `.alph` and `.name` and Value "Romulus Alpha",
- a triplet with Paths `.alph` and `.model` and Value "alph_hunter_mech",
- a triplet with Paths `.nosf` and `.name` and Value "Nosferaudio",
- a triplet with Paths `.nosf` and `.model` and Value "nosf_sound_mech",
- a triplet with Paths `.bear` and `.name` and Value "Honey Bear",
- and so on.

Another step follows in which the JMON Parser, through individual 
concatenations, replaces this list of "Path-Path-Value triplets" with a list of
"Path-Value pairs." For **JMON Example #1**, this list contains:

- A pair with Path `.alph.name` and Value "Romulus Alpha",
- and so on.

Now _this_ list is revisited. The JMON Parser looks at the Path from each pair.
It adds to the root Object (and sometimes its descendants) any Properties and
Values necessary for the Path to refer to an existing Value. Placeholders are
used anywhere that a concrete Value is to be expected. For **JMON Example #1**,
the root Object, after this step, is made to look like this:
```
{
    "alph": { "name": /* placeholder */, "model": /* placeholder */ },
    "nosf": { "name": /* placeholder */, "model": /* placeholder */ },
    "bear": { "name": /* placeholder */, "model": /* placeholder */ }
}
```

This way, a Path like `.alph.name` refers to an existing Value.

During this step, the JMON Parser will return an error if it encounters a Path
which already has a placeholder. This has the effect of checking the list for
duplicate Paths and Paths that are prefixes of other Paths.

Finally, the JMON Parser takes the Value from each pair and uses it to replace a
placeholder within either the root Object or one of its descendants. After this
step, the Parser given **JMON Example #1** has produced a JSON Object with
the same contents seen in **JMON Example #1 as JSON Text**.

Users of the command-line utility `jq` will notice that this process is similar
to the one performed by jq for assignment expressions like the ones in this
code: `{} | .a.b = "c" | .a.d = "e" | .`. This similarity is deliberate, but
there are a few differences. The most important is that the "placeholding" 
process is performed for all Paths before any Values are assigned, which 
means that some assignment sequences which are valid in jq are not valid in 
JMON.

## JMON Example #2 (Paths With Multiple Elements)

The first iteration of the online gallery looks great, but the next day Mr. Fail
has a phone call with a Brand Manager that costs him a few hairs on his head.
They have found a problem with the gallery's design, and it's the very same
problem that Becky had anticipated (and voiced). He calls Becky into a meeting
and admirably admits to error.

The problem is that, as fans know, the mechs are only half the appeal of
_Transylformers_; the other half is the matter of "who you root for." You see,
reader, _Transylformers_ is all about the conflict between the Vehi-Lycans
(predominantly heroic cars cursed to transform into mechs at night) and the
Boltiri Coven (predominantly villainous mechs cursed to transform into cars
during the day). For this gallery to be a viable product, it needs to have an
index structure which groups the characters, first and foremost, by which side
they take in The War of the Inexhaustibles.

Fortunately, as as fans know, the ability to store Values in a Matrix is only
half the the appeal of _JMON_; the other half is the control that Paths
give you over how those Values are stored in JSON structures. Becky updates the
Path Column of the Object Matrix seen in **JMON Example #1** so that the
characters are grouped by faction. The names of the factions are a little
verbose, so she uses their development codenames ("f4c3z" and "h33lz") instead.
While she's at it, she adds two more characters: tragic hero "Mooncry" and 
the game's archvillain and final boss, "Megacula."

Now the JMON Sheet looks like this:

|         | C-0           | C-1             | C-2                |
|---------|---------------|-----------------|--------------------|
| **R-0** | `:{`          | `.name`         | `.model`           |
| **R-1** | `.f4c3z.alph` | `Romulus Alpha` | `alph_hunter_mech` |
| **R-2** | `.h33lz.nosf` | `Nosferaudio`   | `nosf_sound_mech`  |
| **R-3** | `.f4c3z.bear` | `Honey Bear`    | `bear_melee_mech`  |
| **R-4** | `.f4c3z.owl`  | `Mooncry`       | `owl_flying_mech`  |
| **R-5** | `.h33lz.boss` | `Megacula`      | `boss_old_mech`    |

What is happening here? Well, as mentioned in the previous section (which you
should still feel OK about skipping), during the parsing process for Object
Matrices, individual Paths from the Path Column and the Path Row are concatenated
together into larger Paths. But this concatenation can also occur within a
single Cell, as in the case of the Path at Row 1, Column 0, which used to have a
single Element ("alph"), but now has two ("f4c3z" and "alph"). When the 
Parser parses the Value Cell at Row 1, Column 1, this two-element Path is 
concatenated with `.name` to form a three-element Path.

When assigning a Value to a that has Path that has N elements, JMON will make
sure it is surrounded, like the Tootsie Roll center of an onion, by N Objects.
So when a Parser assigns the Value "Romulus Alpha" to the Path
`.f4c3z.alph.name`, it ensures that the Value has three Objects around it:
one with the Property "f4c3z", another inside it with the Property "alph", and a
third inside that with the Property "name". Let's look at the JSON Text.

### JMON Example #2 as JSON Text

```json
{
    "f4c3z": {
        "alph": { "name": "Romulus Alpha", "model": "alph_hunter_mech" },
        "bear": { "name": "Honey Bear", "model": "bear_melee_mech" },
        "owl": { "name": "Mooncry", "model": "owl_flying_mech" }
    },
    "h33lz": {
        "nosf": { "name": "Nosferaudio", "model": "nosf_sound_mech" },
        "boss": { "name": "Megacula", "model": "boss_old_mech" }
    }
}
```

This JSON Text defines an Object with two sub-Objects as Values for the
Properties "f4c3z" and "h33lz". These sub-Objects, in turn, have one Property
for each character belonging the faction. The Values of _these_ Properties are,
as before, JSON Objects with Properties for "name" and "model".

"Awesome work, Becky!" Becky says to herself. But she suspects the gallery is
still missing something. Her suspicions are confirmed the next day, when she
once again meets with Mr. Fail.

## JMON Example #3 (Blank Cells)

The next day, Mr. Fail has a phone call with a Brand Manager, where they tell
him that there's something else that needs to be added to the gallery. Mr. Fail
sweats so much during this call that he has to change his shirt. As fans know,
the mechs and their conflict are only half the appeal of _Transylformers_; the
other half is the more mundane forms the characters take during the daytime.

Mr. Fail asks Becky to update the gallery so that each character page includes
two models: one for the character's nighttime form (nearly always a mech), and
another for the character's daytime form (nearly always a car).

While updating the Object Matrix seen in **JMON Example #2**, Becky realizes that
the character "Mooncry" is a special case. Mooncry is a Vehi-Lycan who, hundreds
of years ago, over-indulged her animalistic nature and lost the ability to
transform back into a golf cart. As such, she has no daytime form. Luckily, JMON
is well-equipped to handle special cases like these:

|         | C-0           | C-1             | C-2               | C-3                |
|---------|---------------|-----------------|-------------------|--------------------|
| **R-0** | `:{`          | `.name`         | `.model.day`      | `.model.night`     |
| **R-1** | `.f4c3z.alph` | `Romulus Alpha` | `alph_greyhound`  | `alph_hunter_mech` |
| **R-2** | `.h33lz.nosf` | `Nosferaudio`   | `nosf_speaker`    | `nosf_sound_mech`  |
| **R-3** | `.f4c3z.bear` | `Honey Bear`    | `bear_honeywagon` | `bear_melee_mech`  |
| **R-4** | `.f4c3z.owl`  | `Mooncry`       |                   | `owl_flying_mech`  |
| **R-5** | `.h33lz.boss` | `Megacula`      | `boss_hearse`     | `boss_old_mech`    |

The empty Cell at Row 4, Column 2 is called a Blank Cell. Becky uses it to
instruct JMON to omit the Path `.f4c3z.owl.model.day` entirely from the
sub-Object produced by the Matrix. Observe in the next section how the Object
at Path `.f4c3z.owl.model` has one Property, while others
like `.f4c3z.alph.model` have two.

### JMON Example #3 as JSON Text

```json
{
    "f4c3z": {
        "alph": {
            "name": "Romulus Alpha",
            "model": { "day": "alph_greyhound", "night": "alph_hunter_mech" }
        },
        "bear": {
            "name": "Honey Bear",
            "model": { "day": "bear_honeywagon", "night": "bear_melee_mech" }
        },
        "owl": {
            "name": "Mooncry",
            "model": { "night": "owl_flying_mech" }
        }
    },
    "h33lz": {
        "nosf": {
            "name": "Nosferaudio",
            "model": { "day": "nosf_speaker", "night": "nosf_sound_mech" }
        },
        "boss": {
            "name": "Megacula",
            "model": { "day": "boss_hearse", "night": "boss_old_mech" }
        }
    }
}
```

Reviewing Becky's work, Mr. Fail is surprised to see that JMON does not
interpret the Blank Cell as a Null Value or as an empty String. As Becky
explains, a Value Cell can specify either of those things (as you will see 
in **JMON Example #5**), but in the semantics of JSON, it's more common to 
simply omit unused Properties entirely.

## JMON Example #4 (More on Blank Cells)

> **NOTE:** This is another section that you can skip and return to later
> (even if you're engrossed by the story so far;
> story beats at the beginning of **JMON Example #5** will make it
> so that ones here might as well have never happened). If you've always
> wanted to skip a section of something with its author's permission, there
> won't be
> another opportunity in this tutorial... who knows when your next chance will
> be?

> **NOTE:** One of the JMON Parser behaviors
> described in this section will seem a little silly until you get to
> **JMON Example #8**. (I figured that this should be it's own note.)

Seeing Becky use a Blank Cell prompts Mr. Fail to ask if there's any other way
to represent a Blank Cell than with, well, a Cell that is blank. Becky tells him
that Cells which contain a Comment are also treated as Blank Cells by a JMON
Parser.

Mr. Fail is exuberant to learn that JMON has Comments and asks Becky if she can
add a Comment to her JMON Sheet above the Value Cell for "Honey Bear." 
He's unsure of whether the character's name is "Honey Bear" or "Honeybear."
Having just re-read the _Transylformers_ brand guidelines, Becky is certain that
it's "Honey Bear" (and that the trademark has already been registered), but
accepts the request anyway, since it offers a good opportunity to demonstrate
both Comments in JMON and more of how JMON handles Blank Cells.

She produces the following JMON Sheet:

|         | C-0           | C-1              | C-2               | C-3                |
|---------|---------------|------------------|-------------------|--------------------|
| **R-0** | `:{`          | `.name`          | `.model.day`      | `.model.night`     |
| **R-1** | `.f4c3z.alph` | `Romulus Alpha`  | `alph_greyhound`  | `alph_hunter_mech` |
| **R-2** | `.h33lz.nosf` | `Nosferaudio`    | `nosf_speaker`    | `nosf_sound_mech`  |
| **R-3** | `.f4c3z.bear` | `// Honeybear??` | `bear_honeywagon` | `bear_melee_mech`  |
| **R-4** |               | `Honey Bear`     |                   |                    |
| **R-5** | `.f4c3z.owl`  | `Mooncry`        |                   | `owl_flying_mech`  |
| **R-6** | `.h33lz.boss` | `Megacula`       | `boss_hearse`     | `boss_old_mech`    |

In it, the Cell at Row 3, Column 2 is a Comment Cell, a type of Blank Cell that
differs from a Blank Cell in that it contains a Comment (and that's it). Comment
Cells always contain text beginning with the "doubleslash diglyph" (`//`). 
Comment Cells are self-contained and do not affect any other Cells in
the same row or column.

In adding the Comment Cell, Becky pushes the Value Cells for "Honey Bear" down
by one row. This is OK. Because the Cell at Row 3, Column 0 has a Blank Cell
below it, Becky is allowed to put Values for `.f4c3z.bear` in either Row 3 or
Row 4.

Then Mr. Fail asks Becky if there's anything she can do to make the the "Honey
Bear"/"Honeybear" dilemma more emphasized in her JMON Sheet. Becky thinks for
a moment, then changes the JMON Sheet to the following:

|         | C-0           | C-1             | C-2               | C-3                |
|---------|---------------|-----------------|-------------------|--------------------|
| **R-0** | `:{`          | `.name`         | `.model.day`      | `.model.night`     |
| **R-1** | `.f4c3z.bear` | `Honey Bear`    | `// Honeybear??`  |                    |
| **R-2** | `.f4c3z.alph` | `Romulus Alpha` | `alph_greyhound`  | `alph_hunter_mech` |
| **R-3** | `.h33lz.nosf` | `Nosferaudio`   | `nosf_speaker`    | `nosf_sound_mech`  |
| **R-4** | `.f4c3z.bear` |                 | `bear_honeywagon` | `bear_melee_mech`  |
| **R-5** | `.f4c3z.owl`  | `Mooncry`       |                   | `owl_flying_mech`  |
| **R-6** | `.h33lz.boss` | `Megacula`      | `boss_hearse`     | `boss_old_mech`    |

Here, the Path `.f4c3z.bear` is present in the Path Column in two different
rows. Row 1 is used only to assign a Value to the Path `.f4c3z.bear.name`,
whereas the Cells for other sub-Properties of `.f4c3z.bear` are Blank. Row 4,
meanwhile, assign Values to both `.f4c3z.bear.model.day` and
`.f4c3z.bear.model.night`. This time, Cell that would assign a Value
to `.f4c3z.bear.name` is Blank, instead.

Mr. Fail is confused. Doesn't this mean that two different Values are being
assigned to the Path? Becky explains that this is not the case. A JMON Parser
only assigns a Value to a Path when it encounters a Value in an Object Matrix's
Interior. As long as the assignments computed this way don't conflict with each
other, the Object Matrix is valid, even if it contains repeated Paths in its Path
Row or Column.

For this Matrix, the sequence of assignments is:

- `.f4c3z.bear.name` gets assigned "Honey Bear"
- `.f4c3z.alph.name` gets assigned "Romulus Alpha"
- (...)
- `.h33lz.nosf.model.night` gets assigned "nosf_sound_mech"
- `.f4c3z.bear.model.day` gets assigned "bear_honeywagon"
- `.f4c3z.bear.model.night` gets assigned "bear_melee_mech"
- `.f4c3z.owl.name` gets assigned "Mooncry"
- (...)

Since no Paths are repeated in this sequence, the Object Matrix is valid. On the
other hand, if the Cell at Row 4, Column 1 has contained any Value ("Honey Bear"
or "Honeybear", doesn't matter), the Matrix would have been invalid, 
because the JMON Parser would generate two assignments to `.f4c3z.bear.name`.

## JMON Example #5 (JSON Literal Cells)

In his next phone call with a Brand Manager, Mr. Fail receives two pieces of
news. The first piece of news is that "Honey Bear" is indeed the correct spelling.
When Mr. Fail hears this, he scratches his chin and recalls one of Megacula's
famous quotes: "there is much to be learned from beasts." (This bit of the story
makes more sense if you read **JMON Example #4**.)

The second piece of news is that the Brand Manager wants him and Becky to add
more features to the gallery. Specifically, he wants each model in the
gallery to display its name and perform a single animation. When Mr. Fail 
hears this, he bites his nails and recalls another of Megacula's famous 
quotes: "Do not put your faith in such trinkets of deceit!" (This bit of the 
story makes more sense if you know
that Mr. Fail's Magic 8-Ball had told him this wouldn't happen.)

After calming down a bit, Mr. Fail realizes that the gallery can reuse code 
from the team's internal "animation player" tool. This will save Becky time, 
but it will require her to provide JSON data of a particular structure.

Mr. Fail passes this information on to Becky, and for the most part, she is 
undeterred. The JSON Values needed by the animation player code are Objects
with two Properties: a String called `id` and an optional Number called 
`speed` (for when the speed is not `1.0`). She produces the following Object 
Matrix, associating each model with a name and an animation:

|         | C-0                 | C-1               | C-2              | C-3                |
|---------|---------------------|-------------------|------------------|--------------------|
| **R-0** | `:{`                | `.name`           | `.animation.id`  | `.animation.speed` |
| **R-1** | `.alph_greyhound`   | `Greyhound`       | `roll_out`       | `::1.3`            |
| **R-2** | `.alph_hunter_mech` | `Hunter Mech`     | `sword_swing`    |                    |
| **R-3** | `.bear_honeywagon`  | `Honeywagon`      | `liquid_spill`   |                    |
| **R-4** | `.bear_melee_mech`  | `Bear Mech`       | `scratch`        | `::0.5`            |
| **R-5** | `.nosf_speaker`     | `Smart Speaker`   | `eavesdrop`      |                    |
| **R-6** | `.nosf_sound_mech`  | `Disco Mech`      | `dance`          | `::0.9`            |
| **R-7** | `.owl_flying_mech`  | `Owl Mech`        | `scratch`        | `::0.7`            |
| **R-8** | `.boss_hearse`      | `Hearse`          | `stealthy_drive` |                    |
| **R-9** | `.boss_old_mech`    | `Creepy Old Mech` | `siphon_gas`     | `::1.5`            |

The Cells beginning with the "double-peepers diglyph" (`::`) are special.
They belong to a class of Value Cells called "JSON Literal Cells," and with them,
Becky can specify JSON Values that are not Strings. When a Parser reads these 
Value Cells, it treats everything after the double-peepers (`::`) as a JSON 
Text, from which it parses a JSON Value. In JMON, JSON Literal Cells are the 
only way to specify JSON Values that are Numbers. The same is true for 
Booleans (`::true` and `::false`), Nulls (`::null`), and empty Strings (`::''`).

> **NOTE:** To be more specific, `::` is the prefix used for 
> _Single-Quoted_ JSON Literal Cells, while `:::` ("triple-peepers")
> is the prefix used for
> _Double-Quoted_ JSON Literal Cells. In a Single-Quoted JSON Literal Cell, 
> Strings are delimited by single quotes (unlike in real JSON), while in a 
> Double-Quoted JSON Literal Cell, Strings are delimited by double quotes 
> (like in real JSON). Single-Quoted JSON Literal Cells are preferred when 
> a JMON Sheet is stored in a CSV file.

The Object Matrix above is fine, but Becky would prefer that it reside in the
same file as the one from **JMON Example #3**. Fortunately, as as fans know, the
ability to store Values in a Matrix (and control the structure those Matrices
produce) is only half the the appeal of _JMON_; the other half is the ability it
gives you to "nest" Matrices, using one as a Value in the Interior of another!

## JMON Example #6 (Nesting)

Becky revises her JMON Sheets so that the Matrix of gallery entries from 
**JMON Example #3** and the Matrix of models from **JMON Example #4** are 
both Values in a parent Matrix's Interior.

This is the resulting JMON Sheet:

|          | C-0           | C-1                 | C-2               | C-3               | C-4                |
|----------|---------------|---------------------|-------------------|-------------------|--------------------|
| **R-0**  | `:{`          | `.`                 |                   |                   |                    |
| **R-1**  | `.characters` | `:{`                | `.name`           | `.model.day`      | `.model.night`     |
| **R-2**  |               | `.f4c3z.alph`       | `Romulus Alpha`   | `alph_greyhound`  | `alph_hunter_mech` |
| **R-3**  |               | `.h33lz.nosf`       | `Nosferaudio`     | `nosf_speaker`    | `nosf_sound_mech`  |
| **R-4**  |               | `.f4c3z.bear`       | `Honey Bear`      | `bear_honeywagon` | `bear_melee_mech`  |
| **R-5**  |               | `.f4c3z.owl`        | `Mooncry`         |                   | `owl_flying_mech`  |
| **R-6**  |               | `.h33lz.boss`       | `Megacula`        | `boss_hearse`     | `boss_old_mech`    |
| **R-7**  | `.models`     | `:{`                | `.name`           | `.animation.id`   | `.animation.speed` |
| **R-8**  |               | `.alph_greyhound`   | `Greyhound`       | `roll_out`        | `::1.3`            |
| **R-9**  |               | `.alph_hunter_mech` | `Hunter Mech`     | `sword_swing`     |                    |
| **R-10** |               | `.bear_honeywagon`  | `Honeywagon`      | `liquid_spill`    |                    |
| **R-11** |               | `.bear_melee_mech`  | `Bear Mech`       | `scratch`         | `::0.5`            |
| **R-12** |               | `.nosf_speaker`     | `Smart Speaker`   | `eavesdrop`       |                    |
| **R-13** |               | `.nosf_sound_mech`  | `Disco Mech`      | `dance`           | `::0.9`            |
| **R-14** |               | `.owl_flying_mech`  | `Owl Mech`        | `scratch`         | `::0.7`            |
| **R-15** |               | `.boss_hearse`      | `Hearse`          | `stealthy_drive`  |                    |
| **R-16** |               | `.boss_old_mech`    | `Creepy Old Mech` | `siphon_gas`      | `::1.5`            |

In JMON, it is possible to use an Object Matrix in any place where a JSON Value
is expected. When a JMON Parser parses the Interior of the outermost Matrix and
reaches the Cell at Row 1, Column 1, it knows that it is looking at the top-left
Cell of an Object Matrix. It parses this Matrix by reading all the Cells in Rows 1
through 6, Columns 1 through 4. Then, it uses the resulting JSON Object as the
Value it assigns to the Path `.characters`.

> **NOTE:** The single dot (`.`) in Row 1, Column 2 indicates an "empty" Path
> contributed no Elements to a concatenated Path. For example, when the Path
> `.characters` from Column 0 is concatenated with `.`, the resulting Path is
> just `.characters` again.

### JMON Example #6 as JSON Text

The following is the JSON Text that a JMON Parser would produce from the
above JMON Sheet, but with a few Values replaced (for brevity's sake) by
"{ ... }":

```json
{
    "characters": {
        "f4c3z": {
            "alph": {
                "name": "Romulus Alpha",
                "model": { "day": "alph_greyhound", "night": "alph_hunter_mech" }
            },
            "bear": "{ ... }",
            "owl": {
                "name": "Mooncry",
                "model": { "night": "owl_flying_mech" }
            }
        },
        "h33lz": {
            "nosf": "{ ... }",
            "boss": "{ ... }"
        }
    },
    "models": {
        "alph_greyhound": {
            "name": "Greyhound",
            "animation": { "id": "roll_out", "speed": 1.3 }
        },
        "alph_hunter_mech": {
            "name": "Hunter Mech",
            "animation": { "id": "sword_swing" }
        },
        "bear_honeywagon": {
            "name": "Honeywagon",
            "animation": { "id": "liquid_spill" }
        },
        "bear_melee_mech": "{ ... }",
        "nosf_speaker": "{ ... }",
        "nosf_sound_mech": "{ ... }",
        "owl_flying_mech": "{ ... }",
        "boss_hearse": "{ ... }",
        "boss_old_mech": "{ ... }"
    }
}
```

In part due to its use of this JSON, the online gallery is now a dazzling
cyber-spectacle. Users can see Honey Bear perform his signature scratch attack,
Mooncry perform a curiously similar scratch attack, and Nosferaudio dance up a
storm. It seems like there's nothing left for Becky to add, but then suddenly
Mr. Fail gets another call from the Brand Manager. The news from this call is so
shocking that it makes Mr. Fail drop his expensive smartphone on a hard surface,
giving it a unsightly crack.

## JMON Example #7 (Array Matrices)

The Brand Manager tells Mr. Fail that, because Romulus Alpha is the main
character of the game, the gallery should feature multiple animations of him in
his nocturnal "Hunter Mech" form. This way, players can preview how it will look
when Romulus Alpha swings his sword, equips a garlic-scented air freshener, and
heroically sacrifices himself to save humankind.

For the animation player to play multiple animations without reloading a model,
it needs those animations (their IDs and speeds) to be stored in a JSON Array.
Mr. Fail, unaware that JMON provides several ways to create JSON Arrays, worries
that this task will be impossible, but Becky assures him that it won't be
difficult at all.

She opens the JMON Sheet from **JMON Example #6** and modifies the Object
Matrix which previously occupied Rows 7 through 16, Columns 1 through 4. 
After a few minutes of work, this is what that Object Matrix looks like:

|          | C-1                 | C-2               | C-3           | C-4                | C-5      |
|----------|---------------------|-------------------|---------------|--------------------|----------|
| **R-7**  | `:{`                | `.name`           | `.animations` |                    |          |
| **R-8**  | `.alph_greyhound`   | `Greyhound`       | `:[`          | `.id`              | `.speed` |
| **R-9**  |                     |                   | `.+`          | `roll_out`         | `::1.3`  |
| **R-10** | `.alph_hunter_mech` | `Hunter Mech`     | `:[`          | `.id`              | `.speed` |
| **R-11** |                     |                   | `.+`          | `sword_swing`      |          |
| **R-12** |                     |                   | `.+`          | `stake_missile`    |          |
| **R-13** |                     |                   | `.+`          | `equip_garlic`     | `::1.2`  |
| **R-14** |                     |                   | `.+`          | `heroic_sacrifice` |          |
| **R-15** | `.bear_honeywagon`  | `Honeywagon`      | `:[`          | `.id`              | `.speed` |
| **R-16** |                     |                   | `.+`          | `liquid_spill`     |          |
| **R-17** | `.bear_melee_mech`  | `Bear Mech`       | `:[`          | `.id`              | `.speed` |
| **R-18** |                     |                   | `.+`          | `scratch`          | `::0.5`  |
| **R-19** | `.nosf_speaker`     | `Smart Speaker`   | `:[`          | `.id`              | `.speed` |
| **R-20** |                     |                   | `.+`          | `eavesdrop`        |          |
| **R-21** | `.nosf_sound_mech`  | `Disco Mech`      | `:[`          | `.id`              | `.speed` |
| **R-22** |                     |                   | `.+`          | `dance`            | `::0.9`  |
| **R-23** | `.owl_flying_mech`  | `Owl Mech`        | `:[`          | `.id`              | `.speed` |
| **R-24** |                     |                   | `.+`          | `scratch`          |          |
| **R-25** |                     |                   | `.$`          |                    | `::0.7`  |
| **R-25** | `.boss_hearse`      | `Hearse`          | `:[`          | `.id`              | `.speed` |
| **R-26** |                     |                   | `.+`          | `stealthy_drive`   |          |
| **R-27** | `.boss_old_mech`    | `Creepy Old Mech` | `:[`          | `.id`              | `.speed` |
| **R-28** |                     |                   | `.+`          | `siphon_gas`       | `::1.5`  |

In the same way JMON allows you to create Object Matrices which represent JSON
Objects, it also allows you to create Array Matrices which represent JSON Arrays.
As can be seen in Column 3, Array Matrices are distinguished by their the Header
Cells which always contain only the "squarefrown diglyph" (`:[`). Like an Object
Matrix, an Array Matrix contains a Path Column (see Rows 10 through 14, Column 3)
and a Path Row (see Row 10, Columns 4 through 5).

Unlike in an Object Matrix, the first Element of any Path in an Array 
Matrix's Path Column can (and must) be either the _Array-Plus Element_ 
(represented by a `+`) or the _Array-Stop Element_ (`$`). Both of these work 
as placeholders which get replaced by Array indices during the parsing 
process. For example, in the Matrix above, while it looks like the Value 
"equip_garlic" is going to be assigned to the Path `.+.id`, inside some 
Object, it's actually going to be assigned to the Path `[2].id`, inside an 
Array.

> **NOTE:** Mr. Fail had a question about this which I'll repeat here. Is it 
> possible to bypass this replacement process, and just have Paths 
> containing Array indices? The answer is no, though I forgive him for asking.
> The Array-Plus and Array-Stop Elements are the only way to express Paths to
> Array elements.

Here's how the JMON Parser performs this replacement: the first time it 
encounters the Array-Plus Element in a particular Cell, it adds an
element to the JSON Array which lies at the preceding Path.
Then, it replaces the Array-Plus Element with the index 
of the element it just added. So the Paths in Rows 11 through 14 have `.+` 
replaced with `[0]`, `[1]`, `[2]`, and `[3]`. This replacement only occurs 
once for each row, so both "equip_garlic" and `1.2` are assigned to 
Properties of the third Array element.

The Array-Stop Element works like the Array-Plus Element, except that it doesn't
cause an element to be added to the Array. Instead, it is replaced with the
index of the last element in the Array. For example, the Path in Row 25 is
replaced with `[0]` since only one element is present in the Array.

A typical Array Matrix will use the Array-Plus Element much more than the
Array-Stop Element. In fact, Becky didn't need to use the Array-Stop Element in
this Matrix at all. She could have used a single row to assign Values to both the
"id" and "speed" Properties of the first Array element, as she had done for all
the other Array Matrices. The reason she didn't was because she was just so
excited for Mr. Fail to learn about the Array-Stop Element.

### JMON Example #7 as JSON Text

Below is the result of parsing the previous example. I have again replaced some
of the Values with "{ ... }" for brevity's sake. Take note of the the
indentation: just as the example is a region of a larger JMON Sheet, so too
is this JSON Text a snippet of a larger one.
```json
    {
        "alph_greyhound": {
            "name": "Greyhound",
            "animations": [
                { "id": "roll_out", "speed": 1.3 }
            ]
        },
        "alph_hunter_mech": {
            "name": "Hunter Mech",
            "animations": [
                { "id": "sword_swing" },
                { "id": "stake_missile" },
                { "id": "equip_garlic", "speed": 1.2 },
                { "id": "heroic_sacrifice" }
            ]
        },
        "bear_honeywagon": {
            "name": "Honeywagon",
            "animations": [ "{ ... }" ]
        },
        "bear_melee_mech": "{ ... }",
        "nosf_speaker": "{ ... }",
        "nosf_sound_mech": "{ ... }",
        "owl_flying_mech": {
            "name": "Owl Mech",
            "animations": [
                { "id": "scratch", "speed": 0.7 }
            ]
        },
        "boss_hearse": "{ ... }",
        "boss_old_mech": "{ ... }"
    }
```

## JMON Example #8 (More of the Array-Plus and Array-Stop Elements)

Mr. Fail is happy to see that JMON can represent JSON Arrays using Array Matrices,
but Becky wants to show him another way to represent them.

She revises **JMON Example #7** so that it uses the Array-Plus Element and
Array-Stop Elements in a different way. Whereas before she used them in several
Array Matrices, this time she uses them in the Object Matrix for "models". And
whereas before they were the first Element of a Path, this time they go in the
middle. Here is the result:

|          | C-1                 | C-2               | C-3                | C-4                   |
|----------|---------------------|-------------------|--------------------|-----------------------|
| **R-7**  | `:{`                | `.name`           | `.animations.+.id` | `.animations.$.speed` |
| **R-8**  | `.alph_greyhound`   | `Greyhound`       | `roll_out`         | `::1.3`               |
| **R-9**  | `.alph_hunter_mech` | `Hunter Mech`     | `sword_swing`      |                       |
| **R-10** | `.alph_hunter_mech` |                   | `stake_missile`    |                       |
| **R-11** | `.alph_hunter_mech` |                   | `equip_garlic`     | `::1.2`               |
| **R-12** | `.alph_hunter_mech` |                   | `heroic_sacrifice` |                       |
| **R-13** | `.bear_honeywagon`  | `Honeywagon`      | `liquid_spill`     |                       |
| **R-14** | `.bear_melee_mech`  | `Bear Mech`       | `scratch`          | `::0.5`               |
| **R-15** | `.nosf_speaker`     | `Smart Speaker`   | `eavesdrop`        |                       |
| **R-16** | `.nosf_sound_mech`  | `Disco Mech`      | `dance`            | `::0.9`               |
| **R-17** | `.owl_flying_mech`  | `Owl Mech`        | `scratch`          | `::0.7`               |
| **R-18** | `.boss_hearse`      | `Hearse`          | `stealthy_drive`   |                       |
| **R-19** | `.boss_old_mech`    | `Creepy Old Mech` | `siphon_gas`       | `::1.5`               |

It parses into the same JSON Text (**JMON Example #7 as JSON Text**) as before.

Becky knows that as long as they are not the first Element of a Path, she can
use both the Array-Plus Element and the Array-Stop Element in Object Matrices.
When she does, the Parser performs the same Array-modifying and
Path-Element-replacing operations as described for **JMON Example #6**.

For example, when parsing the Cell at Row 11, Column 3, the Parser adds an 
element to the Array at `.alph_greyhound.animations`, then replaces the `.+` in
`.alph_greyhound.animations.+.id` with `[2]`.

If you skipped over **JMON Example #4** (with my permission!) you might find it
peculiar that the Path `.alph_hunter_mech` is repeated in Rows 9 through 12. But
an Object Matrix is allowed to repeat Paths in either its Path Row or Column, as
long as there's no repeated Paths in the list of of assignments it generates.

In this case, from Rows 9 through 12, the Parser generates the following assignments:
- Path `.alph_hunter_mech.name` gets assigned the Value "Hunter Mech".
- Path `.alph_hunter_mech.animations[0].id` gets assigned the Value 
  "sword_swing".
- Path `.alph_hunter_mech.animations[1].id` gets assigned the Value 
  "stake_missile".
- Path `.alph_hunter_mech.animations[2].id` gets assigned the Value 
  "equip_garlic".
- Path `.alph_hunter_mech.animations[2].speed` gets assigned the Value `1.2`.
- Path `.alph_hunter_mech.animations[3].id` gets assigned the Value 
  "heroic_sacrifice".

Since each Path in this list is unique, the Object Matrix is valid.

(If you didn't skip **JMON Example #4**, now you know what I was talking about
in that second author's note.)

As you can see, while the Array-Plus and Array-Stop Elements are essential for
Array Matrices, they can also be useful in Object Matrices, since they provide
a convenient way to represent Object Properties that are Arrays, but often
have only one or two elements.

## JMON Example #9 (The Add-Each-To Operator)

Mr. Fail gets another call from the Brand Manager and slips on a banana peel or
something, who cares. The important thing is that Becky is going to show him
another feature of JMON. As she had predicted in her journal, all those chapters
ago, she needs to add new models for the character "Megacula," because while
every other character in _Transylformers_ has just two forms (one if they're
unlucky), a typical _Transylformers_ game will see Megacula transform into at
least five different things. In the upcoming game alone, he turns into a cloud
of smog, disguises himself as a classy coupé de ville, returns to life after a
faked death (wearing a new coat of paint and insisting upon the name "
Galvacard"), transforms into a giant Bat-Demon-Mech for the final battle, and
then, when he is finally defeated, withers away into a pile of tiny handguns.

In other words, she needs the Object at Path `.characters.h33lz.boss.model` to
have Properties for not only "night" and "day", but also "cloud", "disguised",
"reborn", "final_form", and "defeated". In order to accomplish this without
adding five columns to the JMON Sheet, she uses the _Add-Each-To Operator_.

She takes the Object Matrix for `.characters` (last see in Rows 0 through 6 in
**JMON Example #6**) and expands it by two columns and five rows.

|          | C-1           | C-2             | C-3               | C-4                | C-5           | C-6               |
|----------|---------------|-----------------|-------------------|--------------------|---------------|-------------------|
| **R-1**  | `:{`          | `.name`         | `.model.day`      | `.model.night`     | `.model.+*`   |                   |
| **R-2**  | `.f4c3z.alph` | `Romulus Alpha` | `alph_greyhound`  | `alph_hunter_mech` |               |                   |
| **R-3**  | `.h33lz.nosf` | `Nosferaudio`   | `nosf_speaker`    | `nosf_sound_mech`  |               |                   |
| **R-4**  | `.f4c3z.bear` | `Honey Bear`    | `bear_honeywagon` | `bear_melee_mech`  |               |                   |
| **R-5**  | `.f4c3z.owl`  | `Mooncry`       |                   | `owl_flying_mech`  |               |                   |
| **R-6**  | `.h33lz.boss` | `Megacula`      | `boss_hearse`     | `boss_old_mech`    | `:{`          | `.`               |
| **R-7**  |               |                 |                   |                    | `.cloud`      | `boss_cloud`      |
| **R-8**  |               |                 |                   |                    | `.disguised`  | `boss_disguised`  |
| **R-9**  |               |                 |                   |                    | `.reborn`     | `boss_card`       |
| **R-10** |               |                 |                   |                    | `.final_form` | `boss_final_form` |
| **R-11** |               |                 |                   |                    | `.defeated`   | `boss_defeated`   |

The Add-Each-To Operator is placed at the end of a Path in the Path Row only. It
makes it so that the Parser, instead of assigning a Value to the Path, will
instead add each piece of the Value to the existing Value at the Path. When that
Value is an Object, as in this example, the Parser will add each Property of
that Object to the destination (which must be an Object).

In this example, the nested Matrix in Rows 6 through 11, Columns 5 and 6, gets
parsed to a JSON Object with five Properties. Those Properties are appended to
`.h33lz.boss.model`, which goes from having two Properties to having seven.

The Add-Each-To Operator also works when the Value is an Array
(it appends each element to the Array), but not if the
Value is a Number, a Boolean, String, or Null.
The Value from the Interior and the Value
residing at the Path (if there is one) must be of the same type.

> **NOTE:** The Add-Each-To Operator may work with Strings in a future version of JMON.

Having added these five models, Becky returns to the Object Matrix for
"models", adding new rows to name them and give them each one animation. The
result can be see in the **Appendix #1** below. Has Becky finally finished
making changes to her JMON Sheet? Perhaps not. But the next day there is no
phone call from the Brand Manager, who has gone on a vacation.

## Appendix #1 (The Entire Example JMON Sheet, in Full)

Congratulations on making all the way to the end of **JMON by Example**. As 
a reward, I have included in this appendix the entire JMON Sheet that the 
protagonist "Becky Everydame" created over several (seven or nine depending 
on how you count) iterations of the development process:

|          | C-0           | C-1                 | C-2                  | C-3                | C-4                   | C-5           | C-6               |
|----------|---------------|---------------------|----------------------|--------------------|-----------------------|---------------|-------------------|
| **R-0**  | `:{`          | `.`                 |                      |                    |                       |               |                   |
| **R-1**  | `.characters` | `:{`                | `.name`              | `.model.day`       | `.model.night`        | `.model.+*`   |                   |
| **R-2**  |               | `.f4c3z.alph`       | `Romulus Alpha`      | `alph_greyhound`   | `alph_hunter_mech`    |               |                   |
| **R-3**  |               | `.h33lz.nosf`       | `Nosferaudio`        | `nosf_speaker`     | `nosf_sound_mech`     |               |                   |
| **R-4**  |               | `.f4c3z.bear`       | `Honey Bear`         | `bear_honeywagon`  | `bear_melee_mech`     |               |                   |
| **R-5**  |               | `.f4c3z.owl`        | `Mooncry`            |                    | `owl_flying_mech`     |               |                   |
| **R-6**  |               | `.h33lz.boss`       | `Megacula`           | `boss_hearse`      | `boss_old_mech`       | `:{`          | `.`               |
| **R-7**  |               |                     |                      |                    |                       | `.cloud`      | `boss_cloud`      |
| **R-8**  |               |                     |                      |                    |                       | `.disguised`  | `boss_disguised`  |
| **R-9**  |               |                     |                      |                    |                       | `.reborn`     | `boss_card`       |
| **R-10** |               |                     |                      |                    |                       | `.final_form` | `boss_final_form` |
| **R-11** |               |                     |                      |                    |                       | `.defeated`   | `boss_defeated`   |
| **R-12** | `.models`     | `:{`                | `.name`              | `.animations.+.id` | `.animations.$.speed` |               |                   |
| **R-13** |               | `.alph_greyhound`   | `Greyhound`          | `roll_out`         | `::1.3`               |               |                   |
| **R-14** |               | `.alph_hunter_mech` | `Hunter Mech`        | `sword_swing`      |                       |               |                   |
| **R-15** |               | `.alph_hunter_mech` |                      | `stake_missile`    |                       |               |                   |
| **R-16** |               | `.alph_hunter_mech` |                      | `equip_garlic`     | `::1.2`               |               |                   |
| **R-17** |               | `.alph_hunter_mech` |                      | `heroic_sacrifice` |                       |               |                   |
| **R-18** |               | `.bear_honeywagon`  | `Honeywagon`         | `liquid_spill`     |                       |               |                   |
| **R-19** |               | `.bear_melee_mech`  | `Bear Mech`          | `scratch`          | `::0.5`               |               |                   |
| **R-20** |               | `.nosf_speaker`     | `Smart Speaker`      | `eavesdrop`        |                       |               |                   |
| **R-21** |               | `.nosf_sound_mech`  | `Disco Mech`         | `dance`            | `::0.9`               |               |                   |
| **R-22** |               | `.owl_flying_mech`  | `Owl Mech`           | `scratch`          | `::0.7`               |               |                   |
| **R-23** |               | `.boss_hearse`      | `Hearse`             | `stealthy_drive`   |                       |               |                   |
| **R-24** |               | `.boss_old_mech`    | `Creepy Old Mech`    | `siphon_gas`       | `::1.5`               |               |                   |
| **R-25** |               | `.boss_cloud`       | `Smog Cloud`         | `billow`           | `::1.4`               |               |                   |
| **R-26** |               | `.boss_disguised`   | `Coupé de Ville`     | `invite_passenger` |                       |               |                   |
| **R-27** |               | `.boss_card`        | `Galvacard`          | `sweep_cape`       | `::0.8`               |               |                   |
| **R-28** |               | `.boss_final_form`  | `Final Form`         | `breathe_fire`     |                       |               |                   |
| **R-29** |               | `.boss_defeated`    | `Megacula's Remains` | `crumble`          |                       |               |                   |

### The Entire Example JMON Sheet as JSON Text

A JMON Parser will parse the above JMON Sheet into the following JSON Text:
```json
{
    "characters": {
        "f4c3z": {
            "alph": {
                "name": "Romulus Alpha",
                "model": { "day": "alph_greyhound", "night": "alph_hunter_mech" }
            },
            "bear": {
                "name": "Honey Bear",
                "model": { "day": "bear_honeywagon", "night": "bear_melee_mech" }
            },
            "owl": {
                "name": "Mooncry",
                "model": { "night": "owl_flying_mech" }
            }
        },
        "h33lz": {
            "nosf": {
                "name": "Nosferaudio",
                "model": { "day": "nosf_speaker", "night": "nosf_sound_mech" }
            },
            "boss": {
                "name": "Megacula",
                "model": {
                    "day": "boss_hearse",
                    "night": "boss_old_mech",
                    "cloud": "boss_cloud",
                    "disguised": "boss_disguised",
                    "reborn": "boss_card",
                    "final_form": "boss_final_form",
                    "defeated": "boss_defeated"
                }
            }
        }
    },
    "models": {
        "alph_greyhound": { 
            "name": "Greyhound",
            "animations": [ { "id": "roll_out", "speed": 1.3 } ]
        },
        "alph_hunter_mech": {
            "name": "Hunter Mech",
            "animations": [
                { "id": "sword_swing" },
                { "id": "stake_missile" },
                { "id": "equip_garlic", "speed": 1.2 },
                { "id": "heroic_sacrifice" }
            ]
        },
        "bear_honeywagon": {
            "name": "Honeywagon",
            "animations": [ { "id": "liquid_spill" } ]
        },
        "bear_melee_mech": {
            "name": "Bear Mech",
            "animations": [ { "id": "scratch", "speed": 0.5 } ]
        },
        "nosf_speaker": {
            "name": "Smart Speaker",
            "animations": [ { "id": "eavesdrop" } ]
        },
        "nosf_sound_mech": {
            "name": "Disco Mech",
            "animations": [ { "id": "dance", "speed": 0.9 } ]
        },
        "owl_flying_mech": {
            "name": "Owl Mech",
            "animations": [ { "id": "scratch", "speed": 0.7 } ]
        },
        "boss_hearse": {
            "name": "Hearse",
            "animations": [ { "id": "stealthy_drive" } ]
        },
        "boss_old_mech": {
            "name": "Creepy Old Mech",
            "animations": [ { "id": "siphon_gas", "speed": 1.5 } ]
        },
        "boss_cloud": {
            "name": "Smog Cloud",
            "animations": [ { "id": "billow", "speed": 1.4 } ]
        },
        "boss_disguised": {
            "name": "Coupé de Ville",
            "animations": [ { "id": "invite_passenger" } ]
        },
        "boss_card": {
            "name": "Galvacard",
            "animations": [ { "id": "sweep_cape", "speed": 0.8 } ]
        },
        "boss_final_form": {
            "name": "Final Form",
            "animations": [ { "id": "breathe_fire" } ]
        },
        "boss_defeated": {
            "name": "Megacula's Remains",
            "animations": [ { "id": "crumble" } ]
        }
    }
}
```