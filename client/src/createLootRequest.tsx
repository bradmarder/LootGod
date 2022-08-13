import { useState } from 'react';
import './App.css';
import { Row, Col, Alert, Button, Form } from 'react-bootstrap';
import 'bootstrap/dist/css/bootstrap.min.css';
import axios, { AxiosPromise } from 'axios';
import classes from './eqClasses';
import { EQClass } from './eqClass';

const api = process.env.REACT_APP_API_PATH;

export function CreateLootRequest(props: IContext) {

    const [isLoading, setIsLoading] = useState(false);
    const [charName, setCharName] = useState('');
    const [spell, setSpell] = useState('');
    const [currentItem, setCurrentItem] = useState('');
    const [quantity, setQuantity] = useState(1);
    const [eqClass, setClass] = useState('');
    const [lootId, setLootId] = useState(0);

    const hasQtyLoots = props.loots.filter(x => x.quantity > 0);

    const spellSelected = lootId > 0 && props.loots.filter(x => x.id === lootId)[0].isSpell;

    const isCreateLootDisabled =
        hasQtyLoots.length === 0
        || isLoading
        || charName === ''
        || lootId === 0
        || eqClass === ''
        || quantity < 1

        // if a spell is selected, the user *must* enter a spell name
        || (spellSelected && spell == null);

    const createLootRequest = async () => {
        const data = {
            MainName: props.mainName,
            CharacterName: charName,
            Class: classes.indexOf(eqClass as EQClass),
            LootId: lootId,
            Quantity: spellSelected ? 1 : quantity,
            Spell: spellSelected ? spell : null,
            CurrentItem: currentItem,
        };
        setIsLoading(true);
        try {
            await axios.post<{}, AxiosPromise<ILootRequest>>(api + '/CreateLootRequest', data);
        }
        catch {
            // DUPLICATE REQUEST ERROR MESSAGE
            alert('duplicate request for name and loot');
        }
        finally {
            setIsLoading(false);
        }
        setCharName('');
        setLootId(0);
        setQuantity(1);
        setClass('');
    };
    const setLootLogic = (lootId: number) => {

        // if someone selects a spell, they must enter the name of the spell, and the quantity defaults to 
        // ....but then we have to remove the char/lootId unique combo...
        if (lootId > 0 && props.loots.filter(x => x.id === lootId)[0].isSpell) {
            setQuantity(1);
        }

        setLootId(lootId);
    };

    return (
        <Alert variant='primary'>
            <h4>Create Loot Request</h4>
            <Form onSubmit={e => { e.preventDefault(); }}>
                <Row>
                    <Col>
                        <Form.Group className="mb-3">
                            <Form.Label>Character requesting loot (MAIN or ALT name)</Form.Label>
                            <Form.Control type="text" placeholder="Enter name" value={charName} onChange={e => setCharName(e.target.value)} />
                        </Form.Group>
                    </Col>
                    <Col>
                        <Form.Group className="mb-3">
                            <Form.Label>Class</Form.Label>
                            <Form.Select value={eqClass} onChange={e => setClass((e.target as any).value)}>
                                <option>Select Class</option>
                                {classes.map((item, i) =>
                                    <option key={item} value={item}>{item}</option>
                                )}
                            </Form.Select>
                        </Form.Group>
                    </Col>
                </Row>
                <Row>
                    <Col>
                        <Form.Group className="mb-3">
                            <Form.Label>Loot</Form.Label>
                            <Form.Select value={lootId} onChange={e => setLootLogic(Number((e.target as any).value))}>
                                <option value={0}>Select an Item</option>
                                {hasQtyLoots.map(item =>
                                    <option key={item.id} value={item.id}>{item.name}</option>
                                )}
                            </Form.Select>
                        </Form.Group>
                    </Col>
                    <Col>
                        <Form.Group className="mb-3">
                            <Form.Label>Quantity</Form.Label>
                            <Form.Control type="number" disabled={spellSelected || true} placeholder="Quantity" min="1" max="255" value={quantity} onChange={e => setQuantity(Number(e.target.value))} />
                        </Form.Group>
                    </Col>
                </Row>
                {spellSelected &&
                    <Row>
                        <Col>
                            <Form.Group className="mb-3">
                                <Form.Label>Looks like you've selected a spell rune/nugget. You are <strong className={'text-danger'}>*required*</strong> to name the spell/item you want with this rune/nugget.</Form.Label>
                                <Form.Control type="text" placeholder="Spell name" value={spell} onChange={e => setSpell(e.target.value)} />
                            </Form.Group>
                        </Col>
                    </Row>
                }
                <Row>
                    <Col>
                        <Form.Group className="mb-3">
                            <Form.Label>Upgrading From (your current item) <strong className={'text-danger'}>Required for raid-night loot (not rot drops).</strong></Form.Label>
                            <Form.Control type="text" placeholder="Current Item" value={currentItem} onChange={e => setCurrentItem(e.target.value)} />
                        </Form.Group>
                    </Col>
                </Row>
                {props.lootLocked &&
                    <Alert variant={'danger'}>
                        Loot requests are currently locked/disabled. Please check back later!
                    </Alert>
                }
                {!props.lootLocked &&
                    <Button variant='success' disabled={isCreateLootDisabled} onClick={createLootRequest}>Create</Button>
                }
            </Form>
        </Alert>
    );
}