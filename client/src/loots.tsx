import { useState } from 'react';
import './App.css';
import { Alert, Button, Accordion } from 'react-bootstrap';
import 'bootstrap/dist/css/bootstrap.min.css';
import axios from 'axios';
import classes from './eqClasses';

const api = process.env.REACT_APP_API_PATH;

export default function Loots(props: IContext) {

    const [isLoading, setIsLoading] = useState(false);

    const grantLootRequest = async (id: number) => {
        setIsLoading(true);
        try {
            await axios.post(api + '/GrantLootRequest?id=' + id);
        }
        finally {
            setIsLoading(false);
        }
    }

    const incrementLoot = async (id: number) => {
        setIsLoading(true);
        try {
            await axios.post(api + '/IncrementLootQuantity?id=' + id);
        }
        finally {
            setIsLoading(false);
        }
    }

    const decrementLoot = async (id: number) => {
        setIsLoading(true);
        try {
            await axios.post(api + '/DecrementLootQuantity?id=' + id);
        }
        finally {
            setIsLoading(false);
        }
    }

    const ungrantLootRequest = async (id: number) => {
        setIsLoading(true);
        try {
            await axios.post(api + '/UngrantLootRequest?id=' + id);
        }
        finally {
            setIsLoading(false);
        }
    };

    const items = props.loots
        .filter(x => x.quantity > 0)
        .filter(x => props.spell ? x.isSpell : !x.isSpell)

        // subtract the granted loot quantity from the total loot quantity
        .map(item => {
            const grantedLootQty = props.requests
                .filter(x => x.lootId === item.id)
                .filter(x => x.granted)
                .map(x => x.quantity)
                .reduce((x, y) => x + y, 0);

            // clone object to prevent mutating original and causing issues
            const ref = JSON.parse(JSON.stringify(item))
            ref.quantity -= grantedLootQty;
            return ref;
        });

    const getText = (req: ILootRequest) =>
        [
            req.mainName,
            req.characterName,
            req.isAlt ? 'alt' : 'main',
            classes[req.class as any],
            req.spell || req.quantity,
            req.currentItem,
        ].join(' | ');

    return (
        <>
            <h3>Available {(props.spell ? 'Spells/Nuggets' : 'Loots')}</h3>
            {items.length === 0 &&
                <Alert variant='warning'>
                    Looks like there aren't any {(props.spell ? 'spells/nuggets' : 'loots')} available right now
                </Alert>
            }
            {items.length > 0 &&
                <Accordion>
                    {items.map((item, i) =>
                        <Accordion.Item key={item.id} eventKey={i.toString()}>
                            <Accordion.Header>{item.name} | {item.quantity} available | {props.requests.filter(x => x.lootId === item.id).length} request(s)</Accordion.Header>
                            <Accordion.Body>
                                {props.isAdmin &&
                                    <>
                                        {item.quantity === 0 &&
                                            <Alert variant={'warning'}><strong>Grant Disabled</strong> - Already Allotted Maximum Quantity</Alert>
                                        }
                                        <Button variant={'warning'} size={'sm'} disabled={isLoading} onClick={() => incrementLoot(item.id)}>Increment Quantity</Button>
                                        <Button variant={'danger'} size={'sm'} disabled={isLoading || item.quantity === 0} onClick={() => decrementLoot(item.id)}>Decrement Quantity</Button>
                                        <br /><br />
                                        {props.requests.filter(x => x.lootId === item.id).map(req =>
                                            <span key={req.id}>
                                                {getText(req)}
                                                &nbsp;
                                                {props.isAdmin && req.granted &&
                                                    <Button variant={'danger'} disabled={isLoading} onClick={() => ungrantLootRequest(req.id)}>Un-Grant</Button>
                                                }
                                                {props.isAdmin && !req.granted && item.quantity > 0 &&
                                                    <Button variant={'success'} disabled={isLoading} onClick={() => grantLootRequest(req.id)}>Grant</Button>
                                                }
                                                <br />
                                            </span>
                                        )}
                                    </>
                                }
                            </Accordion.Body>
                        </Accordion.Item>
                    )}
                </Accordion>
            }
        </>
    );
}
